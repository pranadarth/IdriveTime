using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace WpfResxTranslator
{
    public static class CsvHelpers
    {
        public static string[] ReadAllLinesPreserve(string path)
        {
            return File.ReadAllLines(path, Encoding.UTF8);
        }

        public static string[] SplitCsvLine(string line)
        {
            if (line == null) return null;
            var result = new List<string>();
            var sb = new StringBuilder();
            bool inQuotes = false;
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (c == '"')
                {
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '"') { sb.Append('"'); i++; }
                    else inQuotes = !inQuotes;
                }
                else if (c == ',' && !inQuotes)
                {
                    result.Add(sb.ToString()); sb.Clear();
                }
                else sb.Append(c);
            }
            result.Add(sb.ToString());
            return result.ToArray();
        }

        public static void WriteCsv(string path, string[] headers, List<string[]> rows)
        {
            using (var w = new StreamWriter(path, false, Encoding.UTF8))
            {
                w.WriteLine(string.Join(",", headers.Select(Escape).ToArray()));
                foreach (var r in rows)
                {
                    var fields = new string[headers.Length];
                    for (int i = 0; i < headers.Length; i++) fields[i] = i < r.Length ? r[i] : string.Empty;
                    w.WriteLine(string.Join(",", fields.Select(Escape).ToArray()));
                }
            }
        }

        private static string Escape(string f)
        {
            if (f == null) return "";
            var s = f.Replace("\"", "\"\"");
            if (s.Contains(",") || s.Contains("") || s.Contains("") || s.Contains('"')) s = '"' + s + '"';
            return s;
        }
    }
}