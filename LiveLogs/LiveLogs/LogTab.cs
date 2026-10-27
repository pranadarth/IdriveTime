using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Threading;

namespace LiveLogs
{
    /// <summary>
    /// One opened log file: tails the file on a background timer and streams new lines
    /// into an HTML view (WebView2) which handles rendering, selection and search.
    /// </summary>
    public sealed class LogTab : IDisposable
    {
        private const int PollIntervalMs = 250;
        private const long InitialTailBytes = 32L * 1024 * 1024; // only load last 32 MB of huge files
        private const int MaxLinesPerMessage = 20000;

        private static Task<CoreWebView2Environment> _sharedEnvironment;

        private readonly Dispatcher _dispatcher;
        private readonly object _pendingLock = new object();
        private readonly List<string> _pendingLines = new List<string>();
        private readonly StringBuilder _partial = new StringBuilder();
        private readonly Decoder _decoder = new UTF8Encoding(false).GetDecoder();
        private readonly byte[] _buffer = new byte[64 * 1024];
        private readonly char[] _chars;

        private Timer _timer;
        private long _position;
        private bool _firstRead = true;
        private bool _skipFirstLine;
        private bool _fileMissing;
        private volatile bool _resetRequested;
        private int _reading;
        private bool _pendingClear;
        private string _pendingPartial = "";
        private bool _sendScheduled;
        private bool _ready;
        private bool _disposed;
        private bool _darkTheme;

        public string FilePath { get; }
        public WebView2 View { get; }
        public Grid Host { get; } = new Grid();
        public TabItem Tab { get; set; }

        /// <summary>Raised on UI thread for host commands from the view: "find", "next", "prev".</summary>
        public event Action<LogTab, string> CommandReceived;

        public LogTab(string filePath, bool darkTheme)
        {
            FilePath = filePath;
            _darkTheme = darkTheme;
            _dispatcher = Dispatcher.CurrentDispatcher;
            _chars = new char[Encoding.UTF8.GetMaxCharCount(_buffer.Length)];

            View = new WebView2
            {
                DefaultBackgroundColor = darkTheme
                    ? System.Drawing.Color.FromArgb(0x1E, 0x1E, 0x1E)
                    : System.Drawing.Color.White
            };
            Host.Children.Add(View);

            InitializeViewAsync();
            _timer = new Timer(_ => Poll(), null, 0, PollIntervalMs);
        }

        private static Task<CoreWebView2Environment> GetEnvironment()
        {
            if (_sharedEnvironment == null)
            {
                string dataFolder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LiveLogs", "WebView2");
                _sharedEnvironment = CoreWebView2Environment.CreateAsync(null, dataFolder);
            }
            return _sharedEnvironment;
        }

        private async void InitializeViewAsync()
        {
            try
            {
                var env = await GetEnvironment();
                await View.EnsureCoreWebView2Async(env);
                if (_disposed) return;

                var settings = View.CoreWebView2.Settings;
                settings.AreBrowserAcceleratorKeysEnabled = false; // no F5 reload / browser find
                settings.IsStatusBarEnabled = false;
                settings.IsZoomControlEnabled = true;
                settings.AreDevToolsEnabled = false;

                View.CoreWebView2.WebMessageReceived += (s, e) =>
                {
                    string cmd = e.TryGetWebMessageAsString();
                    CommandReceived?.Invoke(this, cmd);
                };
                View.CoreWebView2.NavigationCompleted += (s, e) =>
                {
                    _ready = true;
                    SetTheme(_darkTheme);
                    SendPending();
                };
                View.CoreWebView2.NavigateToString(LogViewHtml.Page);
            }
            catch (Exception ex)
            {
                Host.Children.Clear();
                Host.Children.Add(new TextBlock
                {
                    Text = "Unable to initialize WebView2. Make sure the Microsoft Edge WebView2 Runtime is installed.\n\n" + ex.Message,
                    Margin = new System.Windows.Thickness(10),
                    TextWrapping = System.Windows.TextWrapping.Wrap
                });
            }
        }

        #region File tailing (background thread)

        private void Poll()
        {
            if (Interlocked.CompareExchange(ref _reading, 1, 0) != 0) return;
            try
            {
                ReadNewContent();
            }
            catch (IOException)
            {
                // file temporarily locked/rotating; try again on next tick
            }
            catch (UnauthorizedAccessException)
            {
            }
            finally
            {
                Interlocked.Exchange(ref _reading, 0);
            }
        }

        private void ResetReadState()
        {
            _position = 0;
            _partial.Clear();
            _decoder.Reset();
            _skipFirstLine = false;
        }

        private void ReadNewContent()
        {
            if (_disposed) return;

            if (_resetRequested)
            {
                _resetRequested = false;
                ResetReadState();
                QueueClear();
            }

            if (!File.Exists(FilePath))
            {
                if (!_fileMissing)
                {
                    _fileMissing = true;
                    ResetReadState();
                    QueueClear();
                }
                return;
            }
            _fileMissing = false;

            using (var fs = new FileStream(FilePath, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete, 4096, FileOptions.SequentialScan))
            {
                long length = fs.Length;

                if (length < _position)
                {
                    // truncated or rotated
                    ResetReadState();
                    QueueClear();
                }

                if (_firstRead)
                {
                    _firstRead = false;
                    if (length > InitialTailBytes)
                    {
                        _position = length - InitialTailBytes;
                        _skipFirstLine = true;
                    }
                }

                if (length == _position) return;

                fs.Seek(_position, SeekOrigin.Begin);
                var lines = new List<string>();
                int read;
                while ((read = fs.Read(_buffer, 0, _buffer.Length)) > 0)
                {
                    _position += read;
                    int charCount = _decoder.GetChars(_buffer, 0, read, _chars, 0);
                    SplitLines(_chars, charCount, lines);

                    if (lines.Count >= MaxLinesPerMessage)
                    {
                        QueueLines(lines);
                        lines = new List<string>();
                    }
                }
                QueueLines(lines);
            }
        }

        private void SplitLines(char[] chars, int count, List<string> lines)
        {
            int start = 0;
            for (int i = 0; i < count; i++)
            {
                if (chars[i] != '\n') continue;

                int end = i;
                if (end > start && chars[end - 1] == '\r') end--;

                string line;
                if (_partial.Length > 0)
                {
                    _partial.Append(chars, start, end - start);
                    if (_partial.Length > 0 && _partial[_partial.Length - 1] == '\r')
                        _partial.Length--;
                    line = _partial.ToString();
                    _partial.Clear();
                }
                else
                {
                    line = new string(chars, start, end - start);
                }

                if (_skipFirstLine)
                    _skipFirstLine = false;
                else
                    lines.Add(line);

                start = i + 1;
            }
            if (start < count)
                _partial.Append(chars, start, count - start);
        }

        private void QueueClear()
        {
            lock (_pendingLock)
            {
                _pendingLines.Clear();
                _pendingPartial = "";
                _pendingClear = true;
            }
            ScheduleSend();
        }

        private void QueueLines(List<string> lines)
        {
            string partial = _skipFirstLine ? "" : _partial.ToString().TrimEnd('\r');
            lock (_pendingLock)
            {
                if (lines.Count == 0 && partial == _pendingPartial && !_pendingClear) return;
                _pendingLines.AddRange(lines);
                _pendingPartial = partial;
            }
            ScheduleSend();
        }

        private void ScheduleSend()
        {
            lock (_pendingLock)
            {
                if (_sendScheduled) return;
                _sendScheduled = true;
            }
            _dispatcher.BeginInvoke(new Action(SendPending), DispatcherPriority.Background);
        }

        #endregion

        #region UI thread

        private string _lastSentPartial = "";

        private void SendPending()
        {
            string json;
            lock (_pendingLock)
            {
                _sendScheduled = false;
                if (!_ready || _disposed) return;
                if (!_pendingClear && _pendingLines.Count == 0 && _pendingPartial == _lastSentPartial) return;

                var sb = new StringBuilder(_pendingLines.Count * 80 + 64);
                sb.Append("{\"c\":").Append(_pendingClear ? "1" : "0").Append(",\"p\":");
                Json.AppendString(sb, _pendingPartial);
                sb.Append(",\"l\":[");
                for (int i = 0; i < _pendingLines.Count; i++)
                {
                    if (i > 0) sb.Append(',');
                    Json.AppendString(sb, _pendingLines[i]);
                }
                sb.Append("]}");
                json = sb.ToString();

                _lastSentPartial = _pendingPartial;
                _pendingLines.Clear();
                _pendingClear = false;
            }
            View.CoreWebView2.PostWebMessageAsJson(json);
        }

        /// <summary>Clears view and re-reads file from the beginning.</summary>
        public void Reset()
        {
            _resetRequested = true;
            ThreadPool.QueueUserWorkItem(_ => Poll());
        }

        public void SetTheme(bool dark)
        {
            _darkTheme = dark;
            View.DefaultBackgroundColor = dark
                ? System.Drawing.Color.FromArgb(0x1E, 0x1E, 0x1E)
                : System.Drawing.Color.White;
            if (_ready)
                _ = View.ExecuteScriptAsync("api.theme(" + (dark ? "true" : "false") + ")");
        }

        /// <summary>Runs search in the view; returns "current/total".</summary>
        public Task<string> SearchAsync(string query) =>
            RunApiAsync("api.search(" + Json.Quote(query ?? "") + ")");

        public Task<string> NextAsync() => RunApiAsync("api.next()");
        public Task<string> PrevAsync() => RunApiAsync("api.prev()");

        public void Focus()
        {
            View.Focus();
        }

        private async Task<string> RunApiAsync(string script)
        {
            if (!_ready || _disposed) return "";
            string result = await View.ExecuteScriptAsync(script);
            if (string.IsNullOrEmpty(result) || result == "null") return "";
            return result.Trim('"');
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _timer?.Dispose();
            _timer = null;
            View.Dispose();
        }

        #endregion
    }

    internal static class Json
    {
        public static string Quote(string s)
        {
            var sb = new StringBuilder(s.Length + 2);
            AppendString(sb, s);
            return sb.ToString();
        }

        public static void AppendString(StringBuilder sb, string s)
        {
            sb.Append('"');
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    case '\u2028': sb.Append("\\u2028"); break;
                    case '\u2029': sb.Append("\\u2029"); break;
                    default:
                        if (c < 0x20)
                            sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else
                            sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
        }
    }
}
