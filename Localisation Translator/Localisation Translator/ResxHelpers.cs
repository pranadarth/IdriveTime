// File: ResxHelpers.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace WpfResxTranslator
{
    public static class ResxHelpers
    {
        public static Dictionary<string, string> ReadResxToDictionary(string path)
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                var doc = XDocument.Load(path);
                var dataEls = doc.Root?.Elements("data").Where(e => e.Attribute("name") != null && e.Element("value") != null);
                if (dataEls != null)
                {
                    foreach (var e in dataEls)
                    {
                        var k = e.Attribute("name").Value;
                        var v = e.Element("value").Value;
                        if (!string.IsNullOrWhiteSpace(k) && !string.IsNullOrWhiteSpace(v)) dict[k] = v;
                    }
                }
            }
            catch { }
            return dict;
        }

        public static void WriteDictionaryToResx(string path, Dictionary<string, string> dict)
        {
            var doc = new XDocument(new XDeclaration("1.0", "utf-8", null));
            var root = new XElement("root");
            doc.Add(root);
            foreach (var kv in dict)
            {
                var data = new XElement("data");
                data.SetAttributeValue("name", kv.Key);
                data.SetAttributeValue(XName.Get("space", "http://www.w3.org/XML/1998/namespace"), "preserve");
                var value = new XElement("value", kv.Value);
                data.Add(value);
                root.Add(data);
            }
            doc.Save(path);
        }
    }
}
