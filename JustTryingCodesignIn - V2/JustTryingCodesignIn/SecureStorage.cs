using CredentialManagement;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JustTryingCodesignIn
{
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
