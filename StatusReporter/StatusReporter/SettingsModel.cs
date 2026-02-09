using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace StatusReporter
{
    public class SettingsModel
    {
        private const string FolderName = "StatusReporter";
        private const string FileName = "settings.xml";

        public string To { get; set; }
        public string From { get; set; }
        public string Cc { get; set; }
        public string SignatureName { get; set; }

        public static SettingsModel Default()
        {
            return new SettingsModel
            {
                To = "remotepc@idrive.com",
                Cc = "",
                SignatureName = "YourName",
                From = "hemant.marathe@idrive.com"
            };
        }

        private static string GetFilePath()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string folder = Path.Combine(appData, FolderName);
            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
            return Path.Combine(folder, FileName);
        }

        public static SettingsModel Load()
        {
            string path = GetFilePath();
            if (!File.Exists(path)) return Default();

            try
            {
                var doc = XDocument.Load(path);
                var root = doc.Element("settings");
                if (root == null) return Default();

                return new SettingsModel
                {
                    To = (string)root.Element("to") ?? Default().To,
                    Cc = (string)root.Element("cc") ?? Default().Cc,
                    SignatureName = (string)root.Element("signature") ?? Default().SignatureName
                };
            }
            catch
            {
                // If any problem reading file, return defaults
                return Default();
            }
        }

        public void Save()
        {
            string path = GetFilePath();
            var doc = new XDocument(
                new XElement("settings",
                    new XElement("to", To ?? ""),
                    new XElement("cc", Cc ?? ""),
                    new XElement("signature", SignatureName ?? "")
                )
            );
            doc.Save(path);
        }
    }
}