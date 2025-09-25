using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mail;
using System.Net;
using System.Text;
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
using CredentialManagement;
using System.Windows.Forms;
using System.Windows.Resources;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Oauth2.v2;
using Google.Apis.Services;
using Google.Apis.Oauth2.v2.Data;

namespace JustTryingCodesignIn
{
    public partial class MainWindow : Window
    {
        private readonly string[] fixedRecipients = new[]
        {
            "automation.remotepc@gmail.com","remotepc.automation@gmail.com"
           //"darths1110@gmail.com"
        };

        private NotifyIcon _trayIcon;
        private UserCredential _googleCredential;

        public MainWindow()
        {
            try
            {
                InitializeComponent();
                Logger.Log("MainWindow: InitializeComponent done.");

                LoadSavedCredentials();
                LaunchTrayIcon();

                // window XAML Visibility="Hidden", so no flash will occur
                // Check the clipboard once at startup (non-blocking)
                try { pasteClipboard(); } catch (Exception ex) { Logger.LogError(ex, "pasteClipboard-startup"); }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "MainWindow constructor");
            }
        }

        #region Credentials load/save
        private void LoadSavedCredentials()
        {
            try
            {
                var (savedEmail, savedPassword) = SecureStorage.GetCredentials();
                if (!string.IsNullOrEmpty(savedEmail))
                {
                    txtEmail.Text = savedEmail;
                    txtEmail.Visibility = Visibility.Collapsed;
                }
                if (!string.IsNullOrEmpty(savedPassword))
                {
                    txtPassword.Password = savedPassword;
                    txtPassword.Visibility = Visibility.Collapsed;
                }
                Logger.Log("LoadSavedCredentials completed.");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "LoadSavedCredentials");
            }
        }
        #endregion

        #region Tray icon
        private void LaunchTrayIcon()
        {
            try
            {
                Uri uri = new Uri("pack://application:,,,/code.ico");
                StreamResourceInfo info = System.Windows.Application.GetResourceStream(uri);

                _trayIcon = new NotifyIcon
                {
                    Icon = new System.Drawing.Icon(info.Stream), // embed or place beside exe
                    Visible = true,
                    Text = "Code SignIn App"
                };

                _trayIcon.MouseClick += (s, args) =>
                {
                    if (args.Button == System.Windows.Forms.MouseButtons.Left)
                        ShowWindow();
                };

                var menu = new System.Windows.Forms.ContextMenuStrip();
                menu.Items.Add("Show", null, (s, e) => ShowWindow());
                menu.Items.Add("Exit", null, (s, e) => CloseApplication());
                _trayIcon.ContextMenuStrip = menu;

                Logger.Log("Tray icon launched.");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "LaunchTrayIcon");
            }
        }
        #endregion

        #region Show / Hide / Close
        private void ShowWindow()
        {
            Logger.Log("ShowWindow called.");
            Dispatcher.Invoke(() =>
            {
                try
                {
                    ShowInTaskbar = true;
                    if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
                    Visibility = Visibility.Visible;
                    Activate();

                    // After bringing UI up, try auto-paste from clipboard
                    try { pasteClipboard(); } catch (Exception ex) { Logger.LogError(ex, "pasteClipboard-on-show"); }

                    // hide email/password fields if already set
                    if (!string.IsNullOrEmpty(txtEmail.Text)) txtEmail.Visibility = Visibility.Collapsed;
                    if (!string.IsNullOrEmpty(txtPassword.Password)) txtPassword.Visibility = Visibility.Collapsed;
                    ShowStatus("", Brushes.Black, hide: true);

                    Logger.Log("Window displayed successfully.");
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "ShowWindow");
                }
            });
        }

        private void HideWindow()
        {
            Dispatcher.Invoke(() =>
            {
                try
                {
                    Visibility = Visibility.Hidden;
                    ShowInTaskbar = false;
                    Logger.Log("Window hidden.");
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "HideWindow");
                }
            });
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            // Hide on close (keep app running). Use tray Exit to quit.
            e.Cancel = true;
            HideWindow();
            base.OnClosing(e);
        }

        private void CloseApplication()
        {
            try
            {
                Logger.Log("CloseApplication invoked.");
                _trayIcon.Visible = false;
                _trayIcon.Dispose();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "CloseApplication cleanup");
            }
            System.Windows.Application.Current.Shutdown();
        }
        #endregion

        #region Clipboard auto-paste
        private void pasteClipboard()
        {
            Logger.Log("pasteClipboard called.");
            try
            {
                // ensure UI element exists
                Dispatcher.Invoke(() =>
                {
                    txtSharedPath.Focus();
                    txtSharedPath.Text = string.Empty;
                });

                if (!System.Windows.Clipboard.ContainsText())
                {
                    Logger.Log("Clipboard has no text.");
                    return;
                }

                var text = System.Windows.Clipboard.GetText().Trim();
                Logger.Log($"Clipboard text detected: {text}");

                var pattern = @"^\\\\192\.168\.3\.61\\.+";
                if (System.Text.RegularExpressions.Regex.IsMatch(text, pattern))
                {
                    Dispatcher.Invoke(() =>
                    {
                        txtSharedPath.Text = text;
                        txtSharedPath.Select(txtSharedPath.Text.Length, 0);
                    });
                    Logger.Log("Clipboard text matched UNC path and was pasted.");
                }
                else
                {
                    Logger.Log("Clipboard text did not match UNC path.");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "pasteClipboard");
            }
        }
        #endregion

        #region UI helpers
        private void ShowStatus(string message, Brush color, bool hide = false)
        {
            Dispatcher.Invoke(() =>
            {
                txtStatus.Text = message;
                txtStatus.Foreground = color;
                txtStatus.Visibility = hide || string.IsNullOrEmpty(message)
                                        ? Visibility.Collapsed
                                        : Visibility.Visible;
            });
        }
        #endregion

        #region Events: Change, SignIn, Send
        private void btnChange_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            txtEmail.Visibility = Visibility.Visible;
            txtPassword.Visibility = Visibility.Visible;
        }

        private async void BtnGoogleSignIn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ShowStatus("Opening browser for Google sign-in...", Brushes.Black);
                Logger.Log("Google sign-in started.");

                _googleCredential = await GoogleMailHelper.SignInGoogleAsync();
                if (_googleCredential == null)
                {
                    ShowStatus("Sign-in cancelled or failed.", Brushes.Red);
                    Logger.Log("Google sign-in returned null credential.");
                    return;
                }

                // get user info
                var oauth2 = new Oauth2Service(new BaseClientService.Initializer
                {
                    HttpClientInitializer = _googleCredential,
                    ApplicationName = "CodeSignIn"
                });
                var me = await oauth2.Userinfo.V2.Me.Get().ExecuteAsync();
                ShowStatus($"Signed in as {me.Email}", Brushes.Green);
                Logger.Log($"Signed in as {me.Email}");

                // set email field and hide it (you may want to persist)
                Dispatcher.Invoke(() =>
                {
                    txtEmail.Text = me.Email;
                    txtEmail.Visibility = Visibility.Collapsed;
                });
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "BtnGoogleSignIn_Click");
                ShowStatus($"Google sign-in failed: {ex.Message}", Brushes.Red);
            }
        }

        private async void btnSend_Click(object sender, RoutedEventArgs e)
        {
            string userEmail = string.Empty;
            string sharedPath = string.Empty;

            Dispatcher.Invoke(() =>
            {
                userEmail = txtEmail.Text.Trim();
                sharedPath = txtSharedPath.Text.Trim();
            });

            // basic checks
            if (string.IsNullOrEmpty(userEmail))
            {
                ShowStatus("Please sign in (or provide an email).", Brushes.Red);
                return;
            }
            if (string.IsNullOrEmpty(sharedPath))
            {
                ShowStatus("Please provide a shared path.", Brushes.Red);
                return;
            }

            Dispatcher.Invoke(() => btnSend.IsEnabled = false);
            ShowStatus("Sending, please wait...", Brushes.Black);
            Logger.Log($"Send initiated. from={userEmail} body={sharedPath}");

            try
            {
                await SendEmailWithOAuthAsync(userEmail, sharedPath);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "btnSend_Click");
                ShowStatus($"Error: {ex.Message}", Brushes.Red);
            }
            finally
            {
                Dispatcher.Invoke(() => btnSend.IsEnabled = true);
            }
        }
        #endregion

        #region Sending via OAuth (MailKit helper called)
        private async Task SendEmailWithOAuthAsync(string fromEmail, string sharedPath)
        {
            if (_googleCredential == null)
            {
                ShowStatus("Please sign in with Google first.", Brushes.Red);
                return;
            }

            try
            {
                ShowStatus("Sending via Gmail (OAuth2)...", Brushes.Black);
                Logger.Log("SendEmailWithOAuthAsync calling GoogleMailHelper.SendMailWithGoogleAsync");

                await GoogleMailHelper.SendMailWithGoogleAsync(
                    _googleCredential,
                    fromEmail,
                    fixedRecipients,
                    "codesign",
                    sharedPath);

                ShowStatus("Request sent (OAuth).", Brushes.Green);
                Logger.Log("Message sent (OAuth).");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "SendEmailWithOAuthAsync");
                ShowStatus($"Send failed: {ex.Message}", Brushes.Red);
            }
        }
        #endregion
    }
}
