using Google.Apis.Gmail.v1;
using Google.Apis.Gmail.v1.Data;
using MimeKit;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace StatusReporter
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private SettingsModel settings;

        public MainWindow()
        {
            InitializeComponent();
            LoadSettingsAndInit();
        }

        private void LoadSettingsAndInit()
        {
            settings = SettingsModel.Load();
            DateText.Text = DateTime.Now.ToString("dd-MM-yyyy");
            Subtitle.Text = "Quickly create your daily status email";
            // (Optionally) prefill tasks with template lines
            TasksBox.Text = "";
        }

        private void OpenBtn_Click(object sender, RoutedEventArgs e)
        {
            var dateStr = DateTime.Now.ToString("dd-MM-yyyy");
            var taskText = TasksBox.Text?.Trim() ?? "";

            var sb = new StringBuilder();
            sb.AppendLine("Hi Team,");
            sb.AppendLine();
            sb.AppendLine("Task completed today:");
            if (!string.IsNullOrEmpty(taskText))
            {
                sb.AppendLine(taskText);
            }
            else
            {
                sb.AppendLine();
            }
            sb.AppendLine();
            sb.AppendLine("Thank you,");
            sb.AppendLine(string.IsNullOrWhiteSpace(settings.SignatureName) ? SettingsModel.Default().SignatureName : settings.SignatureName);

            string body = Uri.EscapeDataString(sb.ToString());
            string to = Uri.EscapeDataString(string.IsNullOrWhiteSpace(settings.To) ? SettingsModel.Default().To : settings.To);
            string cc = Uri.EscapeDataString(string.IsNullOrWhiteSpace(settings.Cc) ? SettingsModel.Default().Cc : settings.Cc);
            string subject = Uri.EscapeDataString($"Status Report: {dateStr}");

            if(WFHCheckbox.IsChecked == true)
            {
                if (!string.IsNullOrEmpty(cc)) cc += Uri.EscapeDataString(",");
                cc += Uri.EscapeDataString("wfh@idrive.com");
                subject = Uri.EscapeDataString($"WFH Status Report: {dateStr}");
            }

            string gmailUrl = $"https://mail.google.com/mail/?view=cm&fs=1&to={to}&cc={cc}&su={subject}&body={body}";

            try
            {
                Process.Start(gmailUrl); // Will open default browser
            }
            catch
            {
                try
                {
                    Process.Start(new ProcessStartInfo("cmd", $"/c start \"\" \"{gmailUrl}\"") { CreateNoWindow = true });
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Could not open browser: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
        //  Uncomment the below method if you want to send directly via Gmail API instead of opening the browser.
        /* private void OpenBtn_Click(object sender, RoutedEventArgs e)
         {
              GmailService service = null;
             try
             {
                 service = GmailAuthorizationHelper.AuthorizeGmail();
             }
             catch (Exception ex)
             {
                 MessageBox.Show("Gmail authorization failed: " + ex.Message, "Authorization Error", MessageBoxButton.OK, MessageBoxImage.Error);
                 return;
             }

             // Build date, subject and body (same style as your previous implementation)
             var dateStr = DateTime.Now.ToString("dd-MM-yyyy");
             var taskText = TasksBox.Text?.Trim() ?? "";

             var sb = new StringBuilder();
             sb.AppendLine("Hi Team,");
             sb.AppendLine();
             sb.AppendLine("Task completed today:");
             if (!string.IsNullOrEmpty(taskText))
             {
                 sb.AppendLine(taskText);
             }
             else
             {
                 sb.AppendLine();
             }
             sb.AppendLine();
             sb.AppendLine("Thank you,");
             sb.AppendLine(string.IsNullOrWhiteSpace(settings.SignatureName) ? SettingsModel.Default().SignatureName : settings.SignatureName);

             string bodyPlain = sb.ToString();
             string subject = $"Status Report: {dateStr}";

             // Read recipients from settings (fallback to defaults)
             string to = string.IsNullOrWhiteSpace(settings.To) ? SettingsModel.Default().To : settings.To;
             string cc = string.IsNullOrWhiteSpace(settings.Cc) ? SettingsModel.Default().Cc : settings.Cc;

             // Get the authorized sender email address (the account the token belongs to)
             string fromEmail;
             try
             {
                 var profile = service.Users.GetProfile("me").Execute();
                 fromEmail = profile?.EmailAddress ?? SettingsModel.Default().To; // fallback if something odd happens
             }
             catch
             {
                 fromEmail = SettingsModel.Default().From;
             }

             // Now send via Gmail API
             try
             {
                 SendEmailViaGmailApi(service, fromEmail, to, cc, subject, bodyPlain);
                 MessageBox.Show("Status email sent successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);

                 // Optionally clear TasksBox or leave it as is:
                 // TasksBox.Clear();
             }
             catch (Exception ex)
             {
                 MessageBox.Show("Failed to send email: " + ex.Message, "Send Error", MessageBoxButton.OK, MessageBoxImage.Error);
             }
         }*/

        public static void SendEmailViaGmailApi(GmailService service, string fromEmail, string to, string cc, string subject, string bodyText)
        {
            var mime = new MimeMessage();

            // FROM
            mime.From.Add(new MailboxAddress("", fromEmail));

            // TO
            var toList = InternetAddressList.Parse(to);
            foreach (var addr in toList.OfType<MailboxAddress>())
                mime.To.Add(addr);

            // CC
            if (!string.IsNullOrWhiteSpace(cc))
            {
                var ccList = InternetAddressList.Parse(cc);
                foreach (var addr in ccList.OfType<MailboxAddress>())
                    mime.Cc.Add(addr);
            }

            mime.Subject = subject;
            mime.Body = new TextPart("plain") { Text = bodyText };

            // Convert MIME message to base64url for Gmail API
            using (var ms = new MemoryStream())
            {
                mime.WriteTo(ms);
                string base64Url = Convert.ToBase64String(ms.ToArray())
                    .Replace('+', '-')
                    .Replace('/', '_')
                    .TrimEnd('=');

                var message = new Google.Apis.Gmail.v1.Data.Message
                {
                    Raw = base64Url
                };

                service.Users.Messages.Send(message, "me").Execute();
            }
        }

        private void SettingsBtn_Click(object sender, RoutedEventArgs e)
        {
            var win = new SettingsWindow(settings);
            win.Owner = this;
            if (win.ShowDialog() == true)
            {
                // reload settings reference (SettingsWindow saved them)
                settings = SettingsModel.Load();
            }
        }
    }
}