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
            "automation.remotepc@gmail.com","remotepc.automation@gmail.com"
            //"darths1110@gmail.com"
        };
        private NotifyIcon _trayIcon;


        public MainWindow()
        {
            InitializeComponent();
            LoadSavedCredentials();
            LaunchToastPopup();
            pasteClipboard();
        }

        /// <summary>
        /// Loads the stored email credentials (if any) from Windows Credential Manager.
        /// </summary>
        private void LoadSavedCredentials()
        {
            var (savedEmail, savedPassword) = SecureStorage.GetCredentials();
            if (!string.IsNullOrEmpty(savedEmail) && !string.IsNullOrEmpty(savedPassword))
            {
                txtEmail.Text = savedEmail;
                txtPassword.Password = savedPassword;
            }
            else
            {
                txtEmail.Visibility = Visibility.Visible;
                txtPassword.Visibility = Visibility.Visible;
            }
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
                }
            };

            // Add right‐click menu
            var menu = new ContextMenuStrip();
            menu.Items.Add("Show", null, (s, e) => ShowWindow());
            menu.Items.Add("Exit", null, (s, e) => CloseApplication());
            _trayIcon.ContextMenuStrip = menu;
        }

        /// <summary>
        /// Called when the Send button is clicked.
        /// </summary>
        private async void btnSend_Click(object sender, RoutedEventArgs e)
        {
            string userEmail = txtEmail.Text.Trim();
            string userPassword = txtPassword.Password.Trim();
            string sharedPath = txtSharedPath.Text.Trim();


            txtEmail.Visibility = Visibility.Collapsed;
            txtPassword.Visibility = Visibility.Collapsed;

            ShowStatus("", Brushes.Black, hide: true);

            if (string.IsNullOrEmpty(userEmail) || string.IsNullOrEmpty(userPassword) || string.IsNullOrEmpty(sharedPath))
            {
                ShowStatus("Fields cannot be empty.", Brushes.Red);
                return;
            }
            btnSend.IsEnabled = false;
            ShowStatus("Sending, Please wait...", Brushes.Black);
            try
            {
                // Send the email with the shared path
                await SendEmail(userEmail, userPassword, sharedPath);

                // Save the credentials securely for future use
                SecureStorage.SaveCredentials(userEmail, userPassword);
            }
            catch (Exception ex)
            {
                ShowStatus($"Error sending email: {ex.Message}", Brushes.Red);
            }
            finally
            {
                btnSend.IsEnabled = true;
            }
        }

        /// <summary>
        /// Sends an email using the provided credentials, with the shared path as the email body.
        /// </summary>
        private Task SendEmail(string userEmail, string userPassword, string sharedPath)
        {
            return Task.Run(async () =>
            {
                try
                {
                    // Configure SMTP settings for Gmail (adjust if using a different provider)
                    using (var smtpClient = new SmtpClient("smtp.gmail.com")
                    {
                        Port = 587,
                        Credentials = new NetworkCredential(userEmail, userPassword),
                        EnableSsl = true,
                    })
                    {
                        // Workaround to fix EHLO/HELO invalid hostname
                        typeof(SmtpClient)
                            .GetField("clientDomain", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                            ?.SetValue(smtpClient, "localhost");

                        var mailMessage = new MailMessage
                        {
                            From = new MailAddress(userEmail),
                            Subject = "codesign",
                            Body = sharedPath,
                            IsBodyHtml = false,
                        };

                        // Add fixed recipient addresses
                        foreach (var recipient in fixedRecipients)
                        {
                            mailMessage.To.Add(recipient);
                        }

                        await smtpClient.SendMailAsync(mailMessage);

                        Dispatcher.Invoke(() => ShowStatus("Request sent", Brushes.Green));
                    }
                }
                catch (Exception ex)
                {
                    Dispatcher.Invoke(() => ShowStatus($"Error sending email: {ex.Message}", Brushes.Red));
                }
            });
        }

        private void btnChange_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            txtEmail.Visibility = Visibility.Visible;
            txtPassword.Visibility = Visibility.Visible;
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

                txtEmail.Visibility = Visibility.Collapsed;
                txtPassword.Visibility = Visibility.Collapsed;
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

    public static class SecureStorage
    {
        private const string CredentialKey = "CodeSignApp_EmailCredentials"; // Unique key for your app

        /// <summary>
        /// Saves the provided email and password in the Credential Manager.
        /// </summary>
        public static void SaveCredentials(string email, string password)
        {
            using (var cred = new Credential())
            {
                cred.Username = email;
                cred.Password = password;
                cred.Target = CredentialKey;
                cred.Type = CredentialType.Generic;
                cred.PersistanceType = PersistanceType.LocalComputer;
                cred.Save();
            }
        }

        /// <summary>
        /// Retrieves the stored credentials from the Credential Manager.
        /// </summary>
        public static (string email, string password) GetCredentials()
        {
            using (var cred = new Credential { Target = CredentialKey })
            {
                if (cred.Load())
                {
                    return (cred.Username, cred.Password);
                }
            }
            return (null, null); // No credentials found
        }

        /// <summary>
        /// Deletes the stored credentials (if needed).
        /// </summary>
        public static void DeleteCredentials()
        {
            using (var cred = new Credential { Target = CredentialKey })
            {
                cred.Delete();
            }
        }
    }
}