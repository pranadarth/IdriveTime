using Google.Apis.Auth.OAuth2;
using Google.Apis.Util.Store;
using MailKit.Security;
using MimeKit;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace JustTryingCodesignIn
{
    
    public static class GoogleMailHelper
    {
        
        private const string ClientId = "37006422803-a2equg42ak3jp4hga2shufg73rab1479.apps.googleusercontent.com";
        private const string ClientSecret = "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";

        // Full Gmail scope required for SMTP/IMAP/POP access
        private static readonly string[] Scopes = new[] {
    "https://mail.google.com/",
    "openid",
    "email",
    "profile" };

        // Persistent token storage (FileDataStore stores tokens under %APPDATA% by default)
        // You may want to provide a custom IDataStore that encrypts tokens.
        private static readonly string TokenStoreFolder =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GoogleMailTokenStore");

        // helper to construct the FileDataStore
        public static FileDataStore GetFileDataStore() =>
            new FileDataStore(TokenStoreFolder, true);

        public static async Task<UserCredential> SignInGoogleAsync(string userId = "user")
        {
            var clientSecrets = new ClientSecrets { ClientId = ClientId, ClientSecret = ClientSecret };
            var dataStore = GetFileDataStore();

            // Log the path so you can debug on other machines
            Logger.Log($"Google token store path: {TokenStoreFolder}");

            var credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
                clientSecrets,
                Scopes,
                userId,
                CancellationToken.None,
                dataStore).ConfigureAwait(false);

            return credential;
        }

        /// <summary>
        /// Delete the token store folder used by FileDataStore (safe: only deletes that folder).
        /// </summary>
        public static void ClearTokenStore()
        {
            try
            {
                Logger.Log($"Clearing token store at: {TokenStoreFolder}");
                if (Directory.Exists(TokenStoreFolder))
                    Directory.Delete(TokenStoreFolder, true);
                Logger.Log("Token store cleared.");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "ClearTokenStore");
                // swallow – UI will inform user if necessary
            }
        }

        /// <summary>
        /// Sends an email using MailKit and an OAuth2 access token from the UserCredential.
        /// </summary>
        public static async Task SendMailWithGoogleAsync(UserCredential credential, string fromEmail,
                                                         string[] recipients, string subject, string body)
        {
            if (credential == null) throw new ArgumentNullException(nameof(credential));

            // Ensure token is fresh (this will refresh using refresh_token if needed)
            if (credential.Token.IsExpired(credential.Flow.Clock))
                await credential.RefreshTokenAsync(CancellationToken.None).ConfigureAwait(false);

            string accessToken = credential.Token.AccessToken;

            // build message with MimeKit
            var message = new MimeMessage();
            message.From.Add(MailboxAddress.Parse(fromEmail));
            foreach (var r in recipients)
                message.To.Add(MailboxAddress.Parse(r));
            message.Subject = subject;
            message.Body = new TextPart("plain") { Text = body ?? string.Empty };

            using (var client = new MailKit.Net.Smtp.SmtpClient())
            {
                // For demo: accept server certificate (optional, not recommended in prod)
                // client.ServerCertificateValidationCallback = (s,c,h,e) => true;
                client.LocalDomain = "localhost";
                // Connect using STARTTLS
                await client.ConnectAsync("smtp.gmail.com", 587, SecureSocketOptions.StartTls).ConfigureAwait(false);

                // Authenticate using the XOAUTH2 mechanism
                var oauth2 = new MailKit.Security.SaslMechanismOAuth2(fromEmail, accessToken);
                await client.AuthenticateAsync(oauth2).ConfigureAwait(false);

                await client.SendAsync(message).ConfigureAwait(false);
                await client.DisconnectAsync(true).ConfigureAwait(false);
            }
        }
    }

}
