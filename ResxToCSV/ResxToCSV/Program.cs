using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Xml.Linq;
using System.Resources;
using ClosedXML.Excel;
using System.Xml;

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
            Console.WriteLine("  1) RESX -> CSV/XLSX (multi-language, selected files only, fixed header)");
            Console.WriteLine("  2) CSV/XLSX -> RESX (multi-language, create new files)");
            Console.WriteLine("  3) CSV/XLSX -> RESX (append/update existing strings.<code>.resx files)");
            Console.Write("Enter 1, 2 or 3: ");
            var choice = Console.ReadLine()?.Trim();

            switch (choice)
            {
                case "1":
                    ResxToMultiCsv_SelectedOnly();
                    break;
                case "2":
                    CsvToMultiResx();
                    break;
                case "3":
                    CsvToMultiResx_AppendExisting();
                    break;
                default:
                    Console.WriteLine("Invalid choice. Exiting.");
                    break;
            }

            Console.WriteLine();
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
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
        // ------------------------- CSV / XLSX -> RESX (multi-column import using fixed header mapping) -------------------------
        static void CsvToMultiResx()
        {
            using var dlg = new OpenFileDialog
            {
                Title = "Select a .csv or .xlsx file",
                Filter = "CSV files (*.csv)|*.csv|Excel files (*.xlsx)|*.xlsx",
                CheckFileExists = true,
                RestoreDirectory = true
            };

            if (dlg.ShowDialog() != DialogResult.OK)
            {
                Console.WriteLine("No file selected. Exiting.");
                return;
            }

            string inputPath = dlg.FileName;
            var ext = Path.GetExtension(inputPath).ToLowerInvariant();

            // Read lines (either from CSV or XLSX)
            string[] headers;
            List<string[]> rows = new();

            if (ext == ".csv")
            {
                var allLines = File.ReadAllLines(inputPath).Where(l => !string.IsNullOrWhiteSpace(l)).ToArray();
                if (allLines.Length < 2)
                {
                    Console.WriteLine("CSV needs at least one header + one data row. Exiting.");
                    return;
                }
                headers = SplitCsvLine(allLines[0]) ?? throw new Exception("Malformed header");
                for (int i = 1; i < allLines.Length; i++)
                {
                    var fields = SplitCsvLine(allLines[i]);
                    if (fields != null)
                        rows.Add(fields);
                }
            }
            else if (ext == ".xlsx")
            {
                var excelRows = ReadExcelRows(inputPath);
                if (excelRows == null || excelRows.Count < 2)
                {
                    Console.WriteLine("Excel needs at least one header + one data row. Exiting.");
                    return;
                }
                headers = excelRows[0];
                for (int i = 1; i < excelRows.Count; i++)
                    rows.Add(excelRows[i]);
            }
            else
            {
                Console.WriteLine("Unsupported file type. Please select a .csv or .xlsx file.");
                return;
            }

            if (headers.Length < 2)
            {
                Console.WriteLine("Header must have at least 'Key' and one language column.");
                return;
            }

            // Map header label -> language code using LanguageMap; skip unknown headers
            var headerToCode = new Dictionary<int, string>();
            for (int i = 1; i < headers.Length; i++)
            {
                var header = headers[i]?.Trim() ?? "";
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
                Console.WriteLine("⚠️  Input does not contain 'English' column (neutral). The import requires English (neutral). Exiting.");
                return;
            }

            // find english index
            int englishIndex = headerToCode.First(kv => kv.Value == "").Key;

            // Build per-code dictionaries from rows (skip rows where English empty)
            var perCodeEntries = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var code in headerToCode.Values.Distinct(StringComparer.OrdinalIgnoreCase))
                perCodeEntries[code] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            // === FIXED LOOP: use indexed for so we can pad/replace rows safely ===
            for (int ri = 0; ri < rows.Count; ri++)
            {
                var row = rows[ri];

                // Ensure row has at least headers.Length fields - pad with empty strings if needed
                if (row.Length < headers.Length)
                {
                    var padded = new string[headers.Length];
                    Array.Copy(row, padded, row.Length);
                    for (int j = row.Length; j < headers.Length; j++) padded[j] = string.Empty;
                    rows[ri] = padded;
                    row = padded;
                }

                var key = row[0]?.Trim();
                if (string.IsNullOrWhiteSpace(key)) continue;

                // If english index beyond row length (shouldn't happen after pad), skip
                if (englishIndex >= row.Length) continue;

                var englishVal = row[englishIndex]?.Trim();
                if (string.IsNullOrWhiteSpace(englishVal)) continue; // user requested only rows with neutral

                foreach (var kv in headerToCode)
                {
                    int colIndex = kv.Key;
                    string code = kv.Value;
                    var val = (colIndex < row.Length) ? row[colIndex]?.Trim() : null;
                    if (!string.IsNullOrWhiteSpace(val))
                    {
                        perCodeEntries[code][key] = val!;
                    }
                }
            }

            // Write out resx files: for each code present, write strings.resx or strings.<code>.resx
            string dir = Path.GetDirectoryName(inputPath)!;
            foreach (var kv in perCodeEntries)
            {
                string code = kv.Key; // "" for English neutral
                if (kv.Value.Count == 0) continue; // skip empty outputs

                string outName = string.IsNullOrEmpty(code) ? "strings.resx" : $"strings.{code}.resx";
                string outPath = Path.Combine(dir, outName);

                WriteResxFile(outPath, kv.Value);
                Console.WriteLine($"   • Wrote {outName} ({kv.Value.Count} entries)");
            }

            Console.WriteLine("!!Done!!");
        }

        /// <summary>
        /// Reads first worksheet from an Excel file and returns rows as array of string[] (header in row 0).
        /// Uses ClosedXML.
        /// </summary>
        static List<string[]> ReadExcelRows(string excelPath)
        {
            var rows = new List<string[]>();
            using var wb = new ClosedXML.Excel.XLWorkbook(excelPath);
            var ws = wb.Worksheets.First();
            // Determine last used row and column
            var lastRow = ws.LastRowUsed()?.RowNumber() ?? 0;
            var lastCol = ws.LastColumnUsed()?.ColumnNumber() ?? 0;
            if (lastRow == 0 || lastCol == 0) return rows;

            for (int r = 1; r <= lastRow; r++)
            {
                var cells = new List<string>();
                for (int c = 1; c <= lastCol; c++)
                {
                    var v = ws.Cell(r, c).GetString();
                    cells.Add(v ?? string.Empty);
                }
                rows.Add(cells.ToArray());
            }
            return rows;
        }

        /// <summary>
        /// Writes a .resx file using an exact header block and given entries (key->value).
        /// The produced XML matches the header structure you requested.
        /// </summary>
        static void WriteResxFile(string path, Dictionary<string, string> entries)
        {
            // long header comment text exactly as required (shortened here for readability in code — keep full text)
            string headerComment = @" 
    Microsoft ResX Schema 
    
    Version 2.0
    
    The primary goals of this format is to allow a simple XML format 
    that is mostly human readable. The generation and parsing of the 
    various data types are done through the TypeConverter classes 
    associated with the data types.
    
    Example:
    
    ... ado.net/XML headers & schema ...
    <resheader name=""resmimetype"">text/microsoft-resx</resheader>
    <resheader name=""version"">2.0</resheader>
    <resheader name=""reader"">System.Resources.ResXResourceReader, System.Windows.Forms, ...</resheader>
    <resheader name=""writer"">System.Resources.ResXResourceWriter, System.Windows.Forms, ...</resheader>
    <data name=""Name1""><value>this is my long string</value><comment>this is a comment</comment></data>
    <data name=""Color1"" type=""System.Drawing.Color, System.Drawing"">Blue</data>
    <data name=""Bitmap1"" mimetype=""application/x-microsoft.net.object.binary.base64"">
        <value>[base64 mime encoded serialized .NET Framework object]</value>
    </data>
    <data name=""Icon1"" type=""System.Drawing.Icon, System.Drawing"" mimetype=""application/x-microsoft.net.object.bytearray.base64"">
        <value>[base64 mime encoded string representing a byte array form of the .NET Framework object]</value>
        <comment>This is a comment</comment>
    </data>
                
    There are any number of ""resheader"" rows that contain simple 
    name/value pairs.
    
    Each data row contains a name, and value. The row also contains a 
    type or mimetype. Type corresponds to a .NET class that support 
    text/value conversion through the TypeConverter architecture. 
    Classes that don't support this are serialized and stored with the 
    mimetype set.
    
    The mimetype is used for serialized objects, and tells the 
    ResXResourceReader how to depersist the object. This is currently not 
    extensible. For a given mimetype the value must be set accordingly:
    
    Note - application/x-microsoft.net.object.binary.base64 is the format 
    that the ResXResourceWriter will generate, however the reader can 
    read any of the formats listed below.
    
    mimetype: application/x-microsoft.net.object.binary.base64
    value   : The object must be serialized with 
            : System.Runtime.Serialization.Formatters.Binary.BinaryFormatter
            : and then encoded with base64 encoding.
    
    mimetype: application/x-microsoft.net.object.soap.base64
    value   : The object must be serialized with 
            : System.Runtime.Serialization.Formatters.Soap.SoapFormatter
            : and then encoded with base64 encoding.

    mimetype: application/x-microsoft.net.object.bytearray.base64
    value   : The object must be serialized into a byte array 
            : using a System.ComponentModel.TypeConverter
            : and then encoded with base64 encoding.
    ";

            // full xsd:schema block string (we'll use exactly like your required sample)
            string schemaFragment = @"
  <xsd:schema id=""root"" xmlns="""" xmlns:xsd=""http://www.w3.org/2001/XMLSchema"" xmlns:msdata=""urn:schemas-microsoft-com:xml-msdata"">
    <xsd:import namespace=""http://www.w3.org/XML/1998/namespace"" />
    <xsd:element name=""root"" msdata:IsDataSet=""true"">
      <xsd:complexType>
        <xsd:choice maxOccurs=""unbounded"">
          <xsd:element name=""metadata"">
            <xsd:complexType>
              <xsd:sequence>
                <xsd:element name=""value"" type=""xsd:string"" minOccurs=""0"" />
              </xsd:sequence>
              <xsd:attribute name=""name"" use=""required"" type=""xsd:string"" />
              <xsd:attribute name=""type"" type=""xsd:string"" />
              <xsd:attribute name=""mimetype"" type=""xsd:string"" />
              <xsd:attribute ref=""xml:space"" />
            </xsd:complexType>
          </xsd:element>
          <xsd:element name=""assembly"">
            <xsd:complexType>
              <xsd:attribute name=""alias"" type=""xsd:string"" />
              <xsd:attribute name=""name"" type=""xsd:string"" />
            </xsd:complexType>
          </xsd:element>
          <xsd:element name=""data"">
            <xsd:complexType>
              <xsd:sequence>
                <xsd:element name=""value"" type=""xsd:string"" minOccurs=""0"" msdata:Ordinal=""1"" />
                <xsd:element name=""comment"" type=""xsd:string"" minOccurs=""0"" msdata:Ordinal=""2"" />
              </xsd:sequence>
              <xsd:attribute name=""name"" type=""xsd:string"" use=""required"" msdata:Ordinal=""1"" />
              <xsd:attribute name=""type"" type=""xsd:string"" msdata:Ordinal=""3"" />
              <xsd:attribute name=""mimetype"" type=""xsd:string"" msdata:Ordinal=""4"" />
              <xsd:attribute ref=""xml:space"" />
            </xsd:complexType>
          </xsd:element>
          <xsd:element name=""resheader"">
            <xsd:complexType>
              <xsd:sequence>
                <xsd:element name=""value"" type=""xsd:string"" minOccurs=""0"" msdata:Ordinal=""1"" />
              </xsd:sequence>
              <xsd:attribute name=""name"" type=""xsd:string"" use=""required"" />
            </xsd:complexType>
          </xsd:element>
        </xsd:choice>
      </xsd:complexType>
    </xsd:element>
  </xsd:schema>
";

            // Create XML doc
            var root = new XElement("root");

            // Add comment (exact large comment)
            root.Add(new XComment(headerComment));

            // Add schema fragment
            var schemaEl = XElement.Parse(schemaFragment);
            root.Add(schemaEl);

            // Add resheaders exactly as in your required sample
            root.Add(new XElement("resheader", new XAttribute("name", "resmimetype"),
                        new XElement("value", "text/microsoft-resx")));
            root.Add(new XElement("resheader", new XAttribute("name", "version"),
                        new XElement("value", "2.0")));
            // Use the exact reader/writer strings you provided (Version=4.0.0.0)
            root.Add(new XElement("resheader", new XAttribute("name", "reader"),
                        new XElement("value", "System.Resources.ResXResourceReader, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089")));
            root.Add(new XElement("resheader", new XAttribute("name", "writer"),
                        new XElement("value", "System.Resources.ResXResourceWriter, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089")));

            // Add all <data> elements (preserve whitespace in value)
            foreach (var kv in entries)
            {
                var dataEl = new XElement("data",
                    new XAttribute("name", kv.Key),
                    new XAttribute(XNamespace.Xml + "space", "preserve"),
                    new XElement("value", kv.Value)
                );
                root.Add(dataEl);
            }

            var doc = new XDocument(new XDeclaration("1.0", "utf-8", null), root);

            // Save with UTF-8 encoding
            var settings = new System.Xml.XmlWriterSettings
            {
                Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                Indent = true,
                NewLineOnAttributes = false
            };

            using var xw = System.Xml.XmlWriter.Create(path, settings);
            doc.WriteTo(xw);
            xw.Flush();
        }

        // ------------------------- 3) CSV/XLSX -> RESX (append / update existing files) -------------------------
        static void CsvToMultiResx_AppendExisting()
        {
            using var dlg = new OpenFileDialog
            {
                Title = "Select a .csv or .xlsx file to append to existing resx files",
                Filter = "Excel files (*.xlsx)|*.xlsx|CSV files (*.csv)|*.csv",
                CheckFileExists = true,
                RestoreDirectory = true
            };

            if (dlg.ShowDialog() != DialogResult.OK)
            {
                Console.WriteLine("No file selected. Exiting.");
                return;
            }

            string inputPath = dlg.FileName;
            var ext = Path.GetExtension(inputPath).ToLowerInvariant();

            // Read headers & rows (reuse your existing CSV/XLSX reading approach)
            string[] headers;
            List<string[]> rows = new();

            if (ext == ".csv")
            {
                var allLines = File.ReadAllLines(inputPath).Where(l => !string.IsNullOrWhiteSpace(l)).ToArray();
                if (allLines.Length < 2)
                {
                    Console.WriteLine("CSV needs at least one header + one data row. Exiting.");
                    return;
                }
                headers = SplitCsvLine(allLines[0]) ?? throw new Exception("Malformed header");
                for (int i = 1; i < allLines.Length; i++)
                {
                    var fields = SplitCsvLine(allLines[i]);
                    if (fields != null) rows.Add(fields);
                }
            }
            else if (ext == ".xlsx")
            {
                var excelRows = ReadExcelRows(inputPath);
                if (excelRows == null || excelRows.Count < 2)
                {
                    Console.WriteLine("Excel needs at least one header + one data row. Exiting.");
                    return;
                }
                headers = excelRows[0];
                for (int i = 1; i < excelRows.Count; i++) rows.Add(excelRows[i]);
            }
            else
            {
                Console.WriteLine("Unsupported file type. Please select a .csv or .xlsx file.");
                return;
            }

            if (headers.Length < 2)
            {
                Console.WriteLine("Header must have at least 'Key' and one language column.");
                return;
            }

            // Map header label -> language code using LanguageMap; skip unknown headers
            var headerToCode = new Dictionary<int, string>();
            for (int i = 1; i < headers.Length; i++)
            {
                var header = headers[i]?.Trim() ?? "";
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
                Console.WriteLine("⚠️  Input does not contain 'English' column (neutral). The import requires English (neutral). Exiting.");
                return;
            }

            // find english index
            int englishIndex = headerToCode.First(kv => kv.Value == "").Key;

            // Prepare per-code dictionaries (we will collect only values to add/update)
            var perCodeEntries = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var code in headerToCode.Values.Distinct(StringComparer.OrdinalIgnoreCase))
                perCodeEntries[code] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            // iterate rows (use indexed for to pad rows if needed)
            for (int ri = 0; ri < rows.Count; ri++)
            {
                var row = rows[ri];
                if (row.Length < headers.Length)
                {
                    var padded = new string[headers.Length];
                    Array.Copy(row, padded, row.Length);
                    for (int j = row.Length; j < headers.Length; j++) padded[j] = string.Empty;
                    rows[ri] = padded;
                    row = padded;
                }

                var key = row[0]?.Trim();
                if (string.IsNullOrWhiteSpace(key)) continue;

                var englishVal = row[englishIndex]?.Trim();
                if (string.IsNullOrWhiteSpace(englishVal)) continue; // skip if neutral empty

                foreach (var kv in headerToCode)
                {
                    int colIndex = kv.Key;
                    string code = kv.Value;
                    var val = (colIndex < row.Length) ? row[colIndex]?.Trim() : null;
                    if (!string.IsNullOrWhiteSpace(val))
                    {
                        perCodeEntries[code][key] = val!;
                    }
                }
            }

            // Now for each language code, update existing resx if present; if not present, create a new file
            string dir = Path.GetDirectoryName(inputPath)!;
            foreach (var kv in perCodeEntries)
            {
                var code = kv.Key; // "" for English
                if (kv.Value.Count == 0) continue; // nothing to do

                string targetFile = string.IsNullOrEmpty(code) ? Path.Combine(dir, "strings.resx") : Path.Combine(dir, $"strings.{code}.resx");

                UpdateOrCreateResxFile(targetFile, kv.Value);
                Console.WriteLine($"• Updated {Path.GetFileName(targetFile)} ({kv.Value.Count} keys added/updated).");
            }

            Console.WriteLine("**Append/update complete.**");
        }


        /// <summary>
        /// If resx exists, load it and add/update <data> elements per entries (key->value).
        /// If not exists, create a new resx with the exact header block using WriteResxFile.
        /// </summary>
        static void UpdateOrCreateResxFile(string path, Dictionary<string, string> entries)
        {
            if (!File.Exists(path))
            {
                // create brand-new with exact header structure
                WriteResxFile(path, entries);
                return;
            }

            // Load existing doc
            XDocument doc;
            try
            {
                doc = XDocument.Load(path);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Failed to load existing resx '{path}': {ex.Message}. Skipping.");
                return;
            }

            var root = doc.Root;
            if (root == null)
            {
                Console.WriteLine($"⚠️ Malformed resx (no root): {path}. Skipping.");
                return;
            }

            // For easier insertion after headers we attempt to find last <resheader> element index
            var resHeaderEls = root.Elements("resheader").ToList();
            XElement insertionAnchor = resHeaderEls.LastOrDefault() ?? null;

            // Build or update each <data> element
            foreach (var kv in entries)
            {
                string key = kv.Key;
                string val = kv.Value;

                var existing = root.Elements("data").FirstOrDefault(e => (string)e.Attribute("name") == key);
                if (existing != null)
                {
                    // update value element (or create it if missing)
                    var vEl = existing.Element("value");
                    if (vEl == null)
                    {
                        existing.Add(new XElement("value", val));
                    }
                    else
                    {
                        vEl.Value = val;
                    }
                    existing.SetAttributeValue(XNamespace.Xml + "space", "preserve");
                }
                else
                {
                    // create new data element
                    var dataEl = new XElement("data",
                                    new XAttribute("name", key),
                                    new XAttribute(XNamespace.Xml + "space", "preserve"),
                                    new XElement("value", val));
                    if (insertionAnchor != null)
                    {
                        // insert after last resheader (or at end if insertion fails)
                        insertionAnchor.AddAfterSelf(dataEl);
                        insertionAnchor = dataEl; // move anchor so next insert goes after this new node
                    }
                    else
                    {
                        root.Add(dataEl);
                    }
                }
            }

            // Save back preserving UTF-8 without BOM and indentation (same as WriteResxFile)
            var settings = new XmlWriterSettings
            {
                Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                Indent = true,
                NewLineOnAttributes = false
            };

            try
            {
                using var xw = XmlWriter.Create(path, settings);
                doc.WriteTo(xw);
                xw.Flush();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Failed to write resx '{path}': {ex.Message}");
            }
        }


        // ------------------------- Helpers -------------------------
        static string GetBasePrefix(string fileNameWithoutExt)
        {
            // returns text before first '.' if present, otherwise whole string
            var idx = fileNameWithoutExt.IndexOf('.');
            return idx >= 0 ? fileNameWithoutExt.Substring(0, idx) : fileNameWithoutExt;
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
