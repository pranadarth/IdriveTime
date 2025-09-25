using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Xml.Linq;
using System.Resources;
using ClosedXML.Excel;

namespace ResxCsvConverter
{
    class Program
    {
        // Keep this at top of the class
        static readonly (string Display, string Code)[] LanguageMap = new[]
        {
            ("English", ""), // neutral
            ("German", "de"),
            ("Spanish", "es"),
            ("French", "fr"),
            ("Italian", "it"),
            ("Dutch", "nl"),
            ("Portuguese", "pt"),
            ("Portuguese (Brazil)", "pt-br"),
            ("Japanese", "ja"),
            ("Korean", "ko"),
            ("Simplified Chinese", "zh-CN"),
            ("Traditional Chinese", "zh-TW"),
            ("Thai", "th"),
            ("Indonesian (Bahasa)", "id"),
            ("Danish", "da"),
            ("Croatian", "hr"),
            ("Lithuanian", "lt"),
            ("Hungarian", "hu"),
            ("Norwegian", "no"),
            ("Polish", "pl"),
            ("Romanian", "ro"),
            ("Slovak", "sk"),
            ("Serbian", "sr"),
            ("Finnish", "fi"),
            ("Swedish", "sv"),
            ("Vietnamese", "vi"),
            ("Turkish", "tr"),
            ("Czech", "cs"),
            ("Greek", "el"),
            ("Bulgarian", "bg"),
            ("Russian", "ru"),
            ("Ukrainian", "uk")
        };

        [STAThread]
        static void Main()
        {
            Console.WriteLine("Choose an option:");
            Console.WriteLine("  1) RESX -> CSV (multi-language, selected files only, fixed header)");
            Console.WriteLine("  2) CSV -> RESX (multi-language, fixed header mapping)");
            Console.Write("Enter 1 or 2: ");
            var choice = Console.ReadLine()?.Trim();

            switch (choice)
            {
                case "1":
                    ResxToMultiCsv_SelectedOnly();
                    break;
                case "2":
                    CsvToMultiResx();
                    break;
                default:
                    Console.WriteLine("Invalid choice. Exiting.");
                    break;
            }
        }

        // ------------------------- RESX -> CSV (selected files only, fixed header) -------------------------
        static void ResxToMultiCsv_SelectedOnly()
        {
            using var dlg = new OpenFileDialog
            {
                Title = "Select one or more .resx files (will only use the files you select)",
                Filter = "ResX files (*.resx)|*.resx",
                CheckFileExists = true,
                Multiselect = true,
                RestoreDirectory = true
            };

            if (dlg.ShowDialog() != DialogResult.OK)
            {
                Console.WriteLine("No file selected. Exiting.");
                return;
            }

            var selected = dlg.FileNames.Distinct(StringComparer.OrdinalIgnoreCase).ToList();

            // Group by base prefix (text before first '.' in file-without-ext)
            var grouped = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var file in selected)
            {
                var basePrefix = GetBasePrefix(Path.GetFileNameWithoutExtension(file));
                if (!grouped.ContainsKey(basePrefix))
                    grouped[basePrefix] = new List<string>();
                grouped[basePrefix].Add(file);
            }

            foreach (var kv in grouped)
            {
                var basePrefix = kv.Key;
                var files = kv.Value.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                if (files.Count == 0) continue;

                // selectedByCode: code -> file path
                var selectedByCode = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var f in files)
                {
                    var p = Path.GetFileNameWithoutExtension(f); // e.g. "strings" or "strings.fr"
                    if (p.Equals(basePrefix, StringComparison.OrdinalIgnoreCase))
                    {
                        selectedByCode[""] = f; // neutral (English)
                    }
                    else if (p.StartsWith(basePrefix + ".", StringComparison.OrdinalIgnoreCase))
                    {
                        var code = p.Substring(basePrefix.Length + 1); // e.g. "fr" or "pt-PT"
                        selectedByCode[code] = f;
                    }
                }

                // Read per-language dictionaries only for selected codes
                var perLangDict = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
                foreach (var code in selectedByCode.Keys)
                {
                    perLangDict[code] = ReadResxFileToDictionary(selectedByCode[code]);
                }

                // Determine keys
                List<string> orderedKeys;
                if (selectedByCode.ContainsKey(""))
                {
                    var neutralMap = perLangDict[""];
                    if (neutralMap == null || neutralMap.Count == 0)
                    {
                        Console.WriteLine($"⚠️  Skipping base '{basePrefix}' — neutral file contains no valid entries.");
                        continue;
                    }
                    orderedKeys = neutralMap.Keys.ToList();
                }
                else
                {
                    var allKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var dict in perLangDict.Values)
                        foreach (var k in dict.Keys) allKeys.Add(k);
                    orderedKeys = allKeys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase).ToList();
                }

                // Build header (Key + only displays for selected codes)
                var headers = new List<string> { "Key" };
                if (selectedByCode.ContainsKey("") && LanguageMap.Any(x => x.Code == ""))
                    headers.Add(LanguageMap.First(x => x.Code == "").Display);

                foreach (var (display, code) in LanguageMap)
                {
                    if (code == "") continue;
                    if (selectedByCode.ContainsKey(code))
                        headers.Add(display);
                }

                // include any fallback selected codes not in LanguageMap
                foreach (var code in selectedByCode.Keys)
                {
                    if (code == "") continue;
                    bool mapped = LanguageMap.Any(x => string.Equals(x.Code, code, StringComparison.OrdinalIgnoreCase));
                    if (!mapped) headers.Add(code);
                }

                // Prepare Excel output path (same folder as first selected file for this base)
                var outputDir = Path.GetDirectoryName(files[0])!;
                var excelPath = Path.Combine(outputDir, basePrefix + ".xlsx");

                // Use ClosedXML to write a proper .xlsx
                using (var wb = new ClosedXML.Excel.XLWorkbook())
                {
                    var ws = wb.Worksheets.Add("Translations");

                    // write headers
                    for (int c = 0; c < headers.Count; c++)
                    {
                        ws.Cell(1, c + 1).Value = headers[c];
                        ws.Cell(1, c + 1).Style.Font.Bold = true;
                    }

                    // prepare header->code mapping for value retrieval
                    var headerCodes = new List<string?>();
                    headerCodes.Add(null); // Key
                    if (selectedByCode.ContainsKey("")) headerCodes.Add("");
                    foreach (var (display, code) in LanguageMap)
                        if (code != "" && selectedByCode.ContainsKey(code)) headerCodes.Add(code);
                    foreach (var code in selectedByCode.Keys) // fallback codes
                        if (!string.IsNullOrEmpty(code) && !LanguageMap.Any(x => string.Equals(x.Code, code, StringComparison.OrdinalIgnoreCase)))
                            headerCodes.Add(code);

                    // write rows
                    int r = 2;
                    foreach (var key in orderedKeys)
                    {
                        // if neutral exists, skip rows with empty neutral value
                        if (selectedByCode.ContainsKey(""))
                        {
                            if (!perLangDict[""].TryGetValue(key, out var neutralVal) || string.IsNullOrWhiteSpace(neutralVal))
                                continue;
                        }

                        ws.Cell(r, 1).Value = key;
                        for (int ci = 1; ci < headerCodes.Count; ci++)
                        {
                            var code = headerCodes[ci];
                            if (code != null && perLangDict.TryGetValue(code, out var dict) && dict.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v))
                                ws.Cell(r, ci + 1).Value = v;
                            else
                                ws.Cell(r, ci + 1).Value = ""; // empty cell
                        }
                        r++;
                    }

                    // optional: autofit columns so Excel view looks nice
                    ws.Columns().AdjustToContents();

                    // save workbook
                    wb.SaveAs(excelPath);
                }

                Console.WriteLine($"-> Created Excel file for base '{basePrefix}': {excelPath} (columns: {string.Join(", ", headers)})");
            }
        }

        // Read a .resx file (XML) and return dictionary of key->value (skip empty values)
        static Dictionary<string, string> ReadResxFileToDictionary(string resxPath)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                var doc = XDocument.Load(resxPath);
                var dataEls = doc.Root?
                                  .Elements("data")
                                  .Where(e => e.Attribute("name") != null && e.Element("value") != null)
                                  .ToList();
                if (dataEls != null)
                {
                    foreach (var e in dataEls)
                    {
                        var key = e.Attribute("name")!.Value;
                        var val = e.Element("value")!.Value;
                        if (!string.IsNullOrWhiteSpace(key) && !string.IsNullOrWhiteSpace(val))
                            result[key] = val;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Failed to read {resxPath}: {ex.Message}");
            }
            return result;
        }

        // ------------------------- CSV -> RESX (multi-column import using fixed header mapping) -------------------------
        static void CsvToMultiResx()
        {
            using var dlg = new OpenFileDialog
            {
                Title = "Select a .csv file",
                Filter = "CSV files (*.csv)|*.csv",
                CheckFileExists = true,
                RestoreDirectory = true
            };

            if (dlg.ShowDialog() != DialogResult.OK)
            {
                Console.WriteLine("No file selected. Exiting.");
                return;
            }

            string csvPath = dlg.FileName;
            var allLines = File.ReadAllLines(csvPath)
                               .Where(l => !string.IsNullOrWhiteSpace(l))
                               .ToArray();

            if (allLines.Length < 2)
            {
                Console.WriteLine("CSV needs at least one header + one data row. Exiting.");
                return;
            }

            // Parse header into fields
            var headers = SplitCsvLine(allLines[0]) ?? throw new Exception("Malformed header");
            if (headers.Length < 2)
            {
                Console.WriteLine("Header must have at least 'Key' and one language column.");
                return;
            }

            // Map header label -> language code using LanguageMap; skip unknown headers
            var headerToCode = new Dictionary<int, string>();
            for (int i = 1; i < headers.Length; i++)
            {
                var header = headers[i].Trim();
                var mapEntry = LanguageMap.FirstOrDefault(x => string.Equals(x.Display, header, StringComparison.OrdinalIgnoreCase));
                if (mapEntry == default)
                {
                    Console.WriteLine($"⚠️  Header column '{header}' not recognized — that column will be ignored.");
                    continue;
                }
                headerToCode[i] = mapEntry.Code; // "" for English
            }

            if (!headerToCode.Values.Contains(""))
            {
                Console.WriteLine("⚠️  CSV header does not contain 'English' column (neutral). The import requires English (neutral). Exiting.");
                return;
            }

            // Create resx writers for each code present in headerToCode
            string dir = Path.GetDirectoryName(csvPath)!;
            var writers = new Dictionary<string, ResXResourceWriter>(StringComparer.OrdinalIgnoreCase);
            foreach (var code in headerToCode.Values.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                string suffix = string.IsNullOrEmpty(code) ? "strings.resx" : $"strings.{code}.resx";
                writers[code] = new ResXResourceWriter(Path.Combine(dir, suffix));
            }

            // Process rows: require English (neutral) column to be non-empty for a row to be written
            // find index of English column
            int englishIndex = -1;
            for (int i = 1; i < headers.Length; i++)
            {
                if (headerToCode.TryGetValue(i, out var c) && string.IsNullOrEmpty(c))
                {
                    englishIndex = i;
                    break;
                }
            }

            if (englishIndex < 0)
            {
                Console.WriteLine("English column index not found (unexpected). Exiting.");
                return;
            }

            foreach (var line in allLines.Skip(1))
            {
                var fields = SplitCsvLine(line);
                if (fields is null || fields.Length != headers.Length) continue;
                var key = fields[0]?.Trim();
                if (string.IsNullOrWhiteSpace(key)) continue;

                var englishVal = fields[englishIndex]?.Trim();
                if (string.IsNullOrWhiteSpace(englishVal))
                    continue; // skip if neutral is empty (user requested)

                // For each language column present, add resource if value non-empty
                foreach (var kv in headerToCode)
                {
                    int colIndex = kv.Key;
                    string code = kv.Value;
                    var val = fields[colIndex]?.Trim();
                    if (!string.IsNullOrWhiteSpace(val))
                    {
                        writers[code].AddResource(key, val);
                    }
                }
            }

            // Flush & close writers
            foreach (var w in writers.Values)
            {
                w.Generate();
                w.Close();
            }

            Console.WriteLine("# Generated RESX files:");
            foreach (var code in writers.Keys)
            {
                string file = string.IsNullOrEmpty(code) ? "strings.resx" : $"strings.{code}.resx";
                Console.WriteLine($"   • {file}");
            }
        }

        // ------------------------- Helpers -------------------------
        static string GetBasePrefix(string fileNameWithoutExt)
        {
            // returns text before first '.' if present, otherwise whole string
            var idx = fileNameWithoutExt.IndexOf('.');
            return idx >= 0 ? fileNameWithoutExt.Substring(0, idx) : fileNameWithoutExt;
        }

        static string CsvEscape(string field, string delimiter)
        {
            if (field == null) return "\"\"";
            // normalize newlines to keep CSV/TSV consistent
            field = field.Replace("\r\n", "\n").Replace("\r", "\n");

            // if delimiter length > 1 (shouldn't be), use first char for checking
            char delimChar = delimiter.Length > 0 ? delimiter[0] : ',';

            bool containsSpecial = field.Contains(delimChar) || field.Contains('"') || field.Contains('\n') || field.Contains('\r');
            var f = field.Replace("\"", "\"\"");
            if (containsSpecial)
                f = $"\"{f}\"";
            return f;
        }

        // Splits a CSV line into fields, handling quoted commas and double-quotes
        static string[]? SplitCsvLine(string line)
        {
            var result = new List<string>();
            var sb = new StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (c == '"')
                {
                    // doubled quote inside quotes means literal quote
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        sb.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }
                }
                else if (c == ',' && !inQuotes)
                {
                    result.Add(sb.ToString());
                    sb.Clear();
                }
                else
                {
                    sb.Append(c);
                }
            }
            result.Add(sb.ToString());
            return result.Count > 0 ? result.ToArray() : null;
        }
    }
}
