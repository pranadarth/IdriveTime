// AuthorizeGmail.cs
using Google.Apis.Auth.OAuth2;
using Google.Apis.Gmail.v1;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace JustTryingCodesignIn
{
    public static class GmailAuthorizationHelper
    {
        // scopes and app name
        private static readonly string[] Scopes = {  GmailService.Scope.GmailSend,
                                                     GmailService.Scope.GmailCompose };
        private const string ApplicationName = "StatusReporter";

        /// <summary>
        /// Authorizes a user using credentials.json (Desktop OAuth) and returns an authorized GmailService.
        /// Blocks until user consents on the first run (opens browser).
        /// </summary>
        /// <returns>Authorized GmailService instance</returns>
        public static async Task<GmailService> AuthorizeGmail()
        {
            // Path to your client credentials JSON downloaded from Google Cloud Console.
            // Either place credentials.json next to the EXE or provide a full path here.
            const string credentialsFile = "credentials.json";

            if (!File.Exists(credentialsFile))
                throw new FileNotFoundException($"Could not find '{credentialsFile}'. Put your credentials.json next to the EXE or update the path in AuthorizeGmail().");

            using (var stream = new FileStream(credentialsFile, FileMode.Open, FileAccess.Read))
            {
                // token storage path in AppData
                string credPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "StatusReporter", "token.json");

                var clientSecrets = GoogleClientSecrets.Load(stream).Secrets;

                var credential = GoogleWebAuthorizationBroker.AuthorizeAsync(
                    clientSecrets,
                    Scopes,
                    "user",
                    CancellationToken.None,
                    new FileDataStore(Path.GetDirectoryName(credPath), true)).Result;

                // create the Gmail API service
                var service = new GmailService(new BaseClientService.Initializer()
                {
                    HttpClientInitializer = credential,
                    ApplicationName = ApplicationName,
                });

                return service;
            }
        }
    }
}
