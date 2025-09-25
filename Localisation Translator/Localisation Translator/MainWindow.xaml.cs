using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using WpfResxTranslator;

namespace Localisation_Translator
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
  public partial class MainWindow : Window
    {
        private readonly TranslatorService _translator = new TranslatorService();
        private CancellationTokenSource _cts;

        private List<string[]> _csvRows = new List<string[]>();
        private string[] _headers = new string[0];

        public MainWindow()
        {
            InitializeComponent();
            ModelCombo.SelectedIndex = 0;
            PopulateDefaultLanguages();
            ExcludeBox.Text = string.Join("", TranslatorService.DefaultExcludeList);
        }

        private void AppendLog(string text)
        {
            Dispatcher.Invoke(new Action(delegate {
                LogBox.AppendText(text + "");
                LogBox.ScrollToEnd();
            }));
        }

        private void PopulateDefaultLanguages()
        {
            LanguagesPanel.Children.Clear();
            foreach (var kv in TranslatorService.DefaultLanguageMap)
            {
                var cb = new CheckBox { Content = kv.Key + " (" + kv.Value + ")", Tag = kv.Value, IsChecked = true, Margin = new Thickness(2) };
                LanguagesPanel.Children.Add(cb);
            }
        }

        private void BtnLoadCsv_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog { Filter = "CSV files (*.csv)|*.csv", Multiselect = false };
            if (dlg.ShowDialog(this) != true) return;
            LoadCsv(dlg.FileName);
        }

        private void BtnLoadResx_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog { Filter = "ResX files (*.resx)|*.resx", Multiselect = true };
            if (dlg.ShowDialog(this) != true) return;
            var files = dlg.FileNames;
            MergeResxFilesIntoTable(files);
        }

        private void LoadCsv(string path)
        {
            try
            {
                var all = CsvHelpers.ReadAllLinesPreserve(path);
                if (all.Length == 0) return;
                var headerFields = CsvHelpers.SplitCsvLine(all[0]);
                _headers = headerFields ?? new string[0];
                _csvRows = all.Skip(1).Select(line => CsvHelpers.SplitCsvLine(line) ?? new string[0]).ToList();

                var preview = new List<Dictionary<string, string>>();
                int maxPreview = Math.Min(200, _csvRows.Count);
                for (int i = 0; i < maxPreview; i++)
                {
                    var row = new Dictionary<string, string>();
                    for (int c = 0; c < _headers.Length; c++)
                    {
                        var key = _headers[c];
                        var val = c < _csvRows[i].Length ? _csvRows[i][c] : string.Empty;
                        row[key] = val;
                    }
                    preview.Add(row);
                }
                PreviewGrid.ItemsSource = preview;
                AppendLog("Loaded CSV: " + path + " rows: " + _csvRows.Count);
            }
            catch (Exception ex)
            {
                AppendLog("Failed to load CSV: " + ex.Message);
            }
        }

        private void MergeResxFilesIntoTable(string[] files)
        {
            try
            {
                var entriesPerFile = new Dictionary<string, Dictionary<string, string>>();
                var orderedFiles = files.OrderBy(f => System.IO.Path.GetFileNameWithoutExtension(f)).ToArray();
                var headers = new List<string> { "Key" };
                foreach (var f in orderedFiles)
                {
                    var p = System.IO.Path.GetFileNameWithoutExtension(f);
                    string lang = p.Contains('.') ? p.Substring(p.IndexOf('.') + 1) : "NeutralValue";
                    headers.Add(lang);
                    var dict = ResxHelpers.ReadResxToDictionary(f);
                    entriesPerFile[lang] = dict;
                }

                var keys = new List<string>();
                if (entriesPerFile.ContainsKey("NeutralValue"))
                    keys.AddRange(entriesPerFile["NeutralValue"].Keys);
                else
                {
                    var keySet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var kv in entriesPerFile)
                    {
                        foreach (var k in kv.Value.Keys) keySet.Add(k);
                    }
                    keys.AddRange(keySet);
                }

                _headers = headers.ToArray();
                _csvRows = new List<string[]>();

                foreach (var k in keys)
                {
                    var row = new string[headers.Count];
                    row[0] = k;
                    for (int i = 1; i < headers.Count; i++)
                    {
                        var lang = headers[i];
                        if (entriesPerFile.TryGetValue(lang, out var map) && map.TryGetValue(k, out var v))
                            row[i] = v;
                        else
                            row[i] = string.Empty;
                    }
                    _csvRows.Add(row);
                }

                var preview = new List<Dictionary<string, string>>();
                int maxPreview = Math.Min(200, _csvRows.Count);
                for (int i = 0; i < maxPreview; i++)
                {
                    var row = new Dictionary<string, string>();
                    for (int c = 0; c < _headers.Length; c++)
                    {
                        var key = _headers[c];
                        var val = c < _csvRows[i].Length ? _csvRows[i][c] : string.Empty;
                        row[key] = val;
                    }
                    preview.Add(row);
                }
                PreviewGrid.ItemsSource = preview;
                AppendLog("Loaded RESX group into table. Rows: " + _csvRows.Count);
            }
            catch (Exception ex)
            {
                AppendLog("Failed to merge resx files: " + ex.Message);
            }
        }

        private async void BtnStart_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                BtnStart.IsEnabled = false;
                BtnStop.IsEnabled = true;
                _cts = new CancellationTokenSource();

                var apiKey = ApiKeyBox.Password;
                if (UseEnvCheck.IsChecked == true)
                {
                    var env = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
                    if (!string.IsNullOrEmpty(env)) apiKey = env;
                }
                var model = ((ComboBoxItem)ModelCombo.SelectedItem).Content.ToString();

                var langSelections = new List<string>();
                foreach (var child in LanguagesPanel.Children)
                {
                    var cb = child as CheckBox;
                    if (cb != null && cb.IsChecked == true)
                    {
                        var s = cb.Tag as string;
                        if (!string.IsNullOrEmpty(s)) langSelections.Add(s);
                    }
                }

                var excludeList = ExcludeBox.Text.Split(new[] { "", ""}, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).Where(s => s.Length > 0).ToArray();

                _translator.Configure(apiKey, model, langSelections, excludeList);

                await RunTranslationWorkflow(_cts.Token);
            }
            catch (Exception ex)
            {
                AppendLog("Start failed: " + ex.Message);
            }
            finally
            {
                BtnStart.IsEnabled = true;
                BtnStop.IsEnabled = false;
            }
        }

        private void BtnStop_Click(object sender, RoutedEventArgs e)
        {
            if (_cts != null) _cts.Cancel();
            AppendLog("Cancel requested...");
        }

        private async Task RunTranslationWorkflow(CancellationToken ct)
        {
            if (_headers == null || _headers.Length == 0 || _csvRows == null || _csvRows.Count == 0)
            {
                AppendLog("No CSV/RESX data loaded. Load a CSV or RESX files first.");
                return;
            }

            int keyIdx = Array.FindIndex(_headers, h => string.Equals(h, "Key", StringComparison.OrdinalIgnoreCase));
            if (keyIdx < 0) { AppendLog("No Key column found in headers."); return; }

            int englishIdx = Array.FindIndex(_headers, h => string.Equals(h, "English", StringComparison.OrdinalIgnoreCase) || string.Equals(h, "NeutralValue", StringComparison.OrdinalIgnoreCase));
            if (englishIdx < 0) englishIdx = 1;

            var rows = new List<RowItem>();
            for (int i = 0; i < _csvRows.Count; i++)
            {
                var rowArr = _csvRows[i];
                var key = rowArr.Length > keyIdx ? rowArr[keyIdx] : string.Empty;
                var ri = new RowItem();
                ri.Key = key;
                ri.Values = rowArr;
                if (!string.IsNullOrWhiteSpace(ri.Key)) rows.Add(ri);
            }

            long totalTasks = (long)rows.Count * _translator.TargetLanguages.Count;
            long completed = 0;
            ProgressBar.Value = 0;
            ProgressBar.Maximum = Math.Max(1, totalTasks);

            for (int langIndex = 0; langIndex < _translator.TargetLanguages.Count; langIndex++)
            {
                var lang = _translator.TargetLanguages[langIndex];
                AppendLog("Starting translations for: " + lang);

                int langIdx = Array.FindIndex(_headers, h => string.Equals(h, lang, StringComparison.OrdinalIgnoreCase));
                if (langIdx < 0)
                {
                    var headersList = new List<string>(_headers);
                    headersList.Add(lang);
                    _headers = headersList.ToArray();
                    for (int r = 0; r < _csvRows.Count; r++)
                    {
                        var arr = _csvRows[r];
                        Array.Resize(ref arr, _headers.Length);
                        _csvRows[r] = arr;
                    }
                    langIdx = _headers.Length - 1;
                }

                var toTranslate = new List<Tuple<int, string, string>>();
                for (int i = 0; i < rows.Count; i++)
                {
                    var row = rows[i];
                    var existing = row.Values.Length > langIdx ? row.Values[langIdx] : string.Empty;
                    if (!string.IsNullOrWhiteSpace(existing)) { completed++; ProgressBar.Value = completed; UpdateProgressText(completed, totalTasks); continue; }
                    var source = row.Values.Length > englishIdx ? row.Values[englishIdx] : string.Empty;
                    if (string.IsNullOrWhiteSpace(source)) { completed++; ProgressBar.Value = completed; UpdateProgressText(completed, totalTasks); continue; }
                    toTranslate.Add(new Tuple<int, string, string>(i, row.Key, source));
                }

                const int chunkSize = 400;
                for (int offset = 0; offset < toTranslate.Count; offset += chunkSize)
                {
                    if (ct.IsCancellationRequested) { AppendLog("Stopped by user"); return; }
                    var chunk = toTranslate.Skip(offset).Take(chunkSize).ToArray();
                    var payloadRows = new List<Tuple<string, string>>();
                    foreach (var c in chunk) payloadRows.Add(new Tuple<string, string>(c.Item2, c.Item3));

                    var result = await _translator.TranslateBatchAsync(payloadRows.Select(t => (t.Item1, t.Item2)).ToArray(), lang, ct);

                    for (int ci = 0; ci < chunk.Length; ci++)
                    {
                        var c = chunk[ci];
                        if (result.ContainsKey(c.Item2))
                        {
                            var translated = result[c.Item2];
                            var rowArr = _csvRows[c.Item1];
                            if (rowArr.Length <= langIdx) Array.Resize(ref rowArr, _headers.Length);
                            rowArr[langIdx] = translated;
                            _csvRows[c.Item1] = rowArr;
                        }
                        completed++;
                        ProgressBar.Value = completed;
                    }

                    UpdateProgressText(completed, totalTasks);
                }

                AppendLog("Completed language: " + lang);
            }

            AppendLog("Writing outputs...");
            var outDir = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            if (OutCsvChk.IsChecked == true)
            {
                var csvOutPath = System.IO.Path.Combine(outDir, "translated.csv");
                CsvHelpers.WriteCsv(csvOutPath, _headers, _csvRows);
                AppendLog("CSV written: " + csvOutPath);
            }

            if (OutResxChk.IsChecked == true)
            {
                for (int ci = 1; ci < _headers.Length; ci++)
                {
                    var lang = _headers[ci];
                    var dict = new Dictionary<string, string>();
                    for (int ri = 0; ri < _csvRows.Count; ri++)
                    {
                        var key = _csvRows[ri].Length > 0 ? _csvRows[ri][0] : null;
                        if (string.IsNullOrWhiteSpace(key)) continue;
                        var val = _csvRows[ri].Length > ci ? _csvRows[ri][ci] : null;
                        if (!string.IsNullOrWhiteSpace(val)) dict[key] = val;
                    }
                    var fname = string.Equals(lang, "NeutralValue", StringComparison.OrdinalIgnoreCase) ? "strings.resx" : ("strings." + lang + ".resx");
                    var fpath = System.IO.Path.Combine(outDir, fname);
                    ResxHelpers.WriteDictionaryToResx(fpath, dict);
                    AppendLog("Wrote RESX: " + fpath);
                }
            }

            AppendLog("All done.");
            CsvHelpers.WriteCsv(System.IO.Path.Combine(outDir, "preview_export.csv"), _headers, _csvRows);
            AppendLog("Preview export written to desktop.");
        }

        private void UpdateProgressText(long completed, long total)
        {
            Dispatcher.Invoke(new Action(delegate { ProgressText.Text = completed + "/" + total; }));
        }
    }

    class RowItem { public string Key = ""; public string[] Values = new string[0]; }
}

