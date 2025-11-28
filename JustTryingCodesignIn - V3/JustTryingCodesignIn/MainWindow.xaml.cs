using CredentialManagement;
using MimeKit;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Resources;
using System.Windows.Shapes;

namespace JustTryingCodesignIn
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        // Fixed recipient email addresses
        private readonly string[] fixedRecipients = new string[]
        {
            "automation.remotepc@gmail.com","remotepc.automation2@gmail.com"
            //"darths1110@gmail.com"
        };
        private NotifyIcon _trayIcon;

        // P/Invoke to ensure window is restored and focused
        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        private const int SW_RESTORE = 9;

        /// <summary>
        /// Bring the window to the foreground (restore if minimized, activate).
        /// Public so App can call it when signalled.
        /// </summary>
        public void BringToFront()
        {
            try
            {
                // If window is hidden (we hide to tray), show it
                if (this.Visibility != Visibility.Visible)
                {
                    this.Show();
                }

                // If minimized, restore
                if (this.WindowState == WindowState.Minimized)
                {
                    this.WindowState = WindowState.Normal;
                }

                // Ensure the window is visible in taskbar and active
                this.ShowInTaskbar = true;

                // Try to bring to foreground using Win32
                var helper = new WindowInteropHelper(this);
                IntPtr hwnd = helper.Handle;
                if (hwnd != IntPtr.Zero)
                {
                    ShowWindow(hwnd, SW_RESTORE);
                    SetForegroundWindow(hwnd);
                }

                // WPF activation fiddles to ensure focus
                this.Activate();

                // Trick to force topmost briefly then turn off so it pops above other windows
                this.Topmost = true;
                this.Topmost = false;
            }
            catch
            {
                // ignore errors — best effort only
            }
        }

        public MainWindow()
        {
            InitializeComponent();
            LaunchToastPopup();
            pasteClipboard();
        }


        private void LaunchToastPopup()
        {
            // Create tray icon
            Uri uri = new Uri("pack://application:,,,/code.ico");
            StreamResourceInfo info = System.Windows.Application.GetResourceStream(uri);

            _trayIcon = new NotifyIcon
            {
                Icon = new System.Drawing.Icon(info.Stream), // embed or place beside exe
                Visible = true,
                Text = "Code SignIn App"
            };

            // Show window on left‐click
            _trayIcon.MouseClick += (s, args) =>
            {
                if (args.Button == MouseButtons.Left)
                {
                    ShowWindow();
                    Logger.Log("Tray icon left-clicked, window shown.");
                }
            };

            // Add right‐click menu
            var menu = new ContextMenuStrip();
            menu.Items.Add("Show", null, (s, e) => ShowWindow());
            menu.Items.Add("Exit", null, (s, e) => CloseApplication());
            _trayIcon.ContextMenuStrip = menu;
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            // Get current screen (handles multi-monitor)
            var screen = System.Windows.Forms.Screen.FromHandle(
                new System.Windows.Interop.WindowInteropHelper(this).Handle);

            double dpiX = 1.0;
            var source = PresentationSource.FromVisual(this);
            if (source?.CompositionTarget != null)
                dpiX = source.CompositionTarget.TransformToDevice.M11;

            double screenWidth = screen.WorkingArea.Width / dpiX;
            double screenHeight = screen.WorkingArea.Height / dpiX;

            // Final position (some padding from bottom-right)
            this.Left = screenWidth - this.ActualWidth - 20;
            this.Top = screenHeight - this.ActualHeight - 20;
        }

        /// <summary>
        /// Called when the Send button is clicked.
        /// </summary>
        private async void btnSend_Click(object sender, RoutedEventArgs e)
        {
            string sharedPath = txtSharedPath.Text.Trim();


            ShowStatus("", Brushes.Black, hide: true);

            if (string.IsNullOrEmpty(sharedPath))
            {
                ShowStatus("Fields cannot be empty.", Brushes.Red);
                return;
            }
            btnSend.IsEnabled = false;
            ShowStatus("Sending, Please wait...", Brushes.Black);
            await Task.Yield();
            try
            {
                // Send the email with the shared path
                await SendEmail(sharedPath).ConfigureAwait(false);

                Dispatcher.Invoke(() => ShowStatus("Request sent", Brushes.Green));
                Logger.Log($"Email sent successfully with path: {sharedPath}");
            }
            catch (Exception ex)
            {
                ShowStatus($"Error sending email: {ex.Message}", Brushes.Red);
                Logger.LogError(ex, "btnSend_Click");
            }
            finally
            {
                Dispatcher.Invoke(() => btnSend.IsEnabled = true);
            }
        }

        /// <summary>
        /// Sends an email using the provided credentials, with the shared path as the email body.
        /// </summary>
        private async Task SendEmail( string sharedPath, CancellationToken cancellationToken = default)
        {
            try
            {
                // 1) Authorize using OAuth (this opens browser on first run)
                var service = await GmailAuthorizationHelper.AuthorizeGmail();

                // 2) Determine the authenticated sender email (profile)
                string fromEmail = null;
                try
                {
                    var profile = await service.Users.GetProfile("me").ExecuteAsync(cancellationToken).ConfigureAwait(false);
                    fromEmail = profile?.EmailAddress;
                }
                catch
                {
                    // fallback - if you want to force authorization for a specific account, handle here
                    fromEmail = null;
                }

                if (string.IsNullOrEmpty(fromEmail))
                {
                    Dispatcher.Invoke(() => ShowStatus("Could not determine sender email from authenticated account.", Brushes.Red));
                    Logger.Log("SendEmail: failed to get profile email.");
                    return;
                }

                // 3) Build MIME message using MimeKit
                var mime = new MimeMessage();
                mime.From.Add(new MailboxAddress("CodeSign", fromEmail));

                    
                foreach (var r in fixedRecipients)
                {
                    var parsed = InternetAddressList.Parse(r).OfType<MailboxAddress>().FirstOrDefault();
                    if (parsed != null) mime.To.Add(parsed);
                }

                mime.Subject = "codesign";
                // Body = the shared path
                mime.Body = new TextPart("plain") { Text = sharedPath };

                // 4) Convert to base64url and send via Gmail API
                using (var ms = new MemoryStream())
                {
                    mime.WriteTo(ms);
                    string base64Url = Convert.ToBase64String(ms.ToArray())
                        .Replace('+', '-').Replace('/', '_').TrimEnd('=');

                    var gmsg = new Google.Apis.Gmail.v1.Data.Message { Raw = base64Url };
                    await service.Users.Messages.Send(gmsg, "me").ExecuteAsync(cancellationToken).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() => ShowStatus($"Error sending email: {ex.Message}", Brushes.Red));
                Logger.LogError(ex, "SendEmail (Gmail API)");
            }
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            // If user tries to close via window chrome, just hide
            HideWindow();
            e.Cancel = true;
            base.OnClosing(e);
        }

        private void ShowStatus(string message, Brush color, bool hide = false)
        {
            txtStatus.Text = message;
            txtStatus.Foreground = color;
            txtStatus.Visibility = hide || string.IsNullOrEmpty(message)
                                    ? Visibility.Collapsed
                                    : Visibility.Visible;
        }

        private void ShowWindow()
        {
            this.Dispatcher.Invoke(() =>
            {
                ShowInTaskbar = true;
                if (this.WindowState == WindowState.Minimized)
                    this.WindowState = WindowState.Normal;
                this.Visibility = Visibility.Visible;
                this.Show();
                this.Activate();
                pasteClipboard();
              
                ShowStatus("", Brushes.Black, hide: true);

            });
        }

        private void HideWindow()
        {
            Dispatcher.Invoke(() =>
            {
                Visibility = Visibility.Hidden;
                ShowInTaskbar = false;
            });
        }

        private void CloseApplication()
        {
            Logger.Log("Application exiting via tray menu.");
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
            System.Windows.Application.Current.Shutdown();
        }

        private void pasteClipboard()
        {
            txtSharedPath.Focus();
            txtSharedPath.Text = string.Empty;

            if (!System.Windows.Clipboard.ContainsText())
                return;

            var text = System.Windows.Clipboard.GetText().Trim();

            // Regex: starts with \\192.168.3.61\ and then at least one character
            var pattern = @"^\\\\192\.168\.3\.61\\.+";
            if (System.Text.RegularExpressions.Regex.IsMatch(text, pattern))
            {
                txtSharedPath.Text = text;
                txtSharedPath.Select(txtSharedPath.Text.Length, 0);
            }
        }

    }

}