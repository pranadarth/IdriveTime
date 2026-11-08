using System.Data;
using System.Globalization;
using System.Text;
using ExcelDataReader;
using PranaWealthOS.Models;

namespace PranaWealthOS.Services
{
    // Parses bank / card statements (CSV, XLS, XLSX) from most Indian banks by sniffing the header row.
    public static class StatementParser
    {
        private static readonly string[] DateHeaders = { "txn date", "transaction date", "date", "value date", "tran date", "posting date" };
        private static readonly string[] DescHeaders = { "narration", "description", "particulars", "transaction details", "remarks", "details", "transaction remarks" };
        private static readonly string[] DebitHeaders = { "withdrawal amt", "withdrawal amount", "withdrawal", "debit amount", "debit", "dr amount", "dr" };
        private static readonly string[] CreditHeaders = { "deposit amt", "deposit amount", "deposit", "credit amount", "credit", "cr amount", "cr" };
        private static readonly string[] AmountHeaders = { "amount", "transaction amount", "amount (inr)", "amt" };
        private static readonly string[] TypeHeaders = { "dr/cr", "cr/dr", "type", "debit/credit" };

        private static readonly string[] DateFormats =
        {
            "dd/MM/yy", "dd/MM/yyyy", "d/M/yyyy", "d/M/yy", "dd-MM-yyyy", "dd-MM-yy", "dd-MMM-yyyy", "dd-MMM-yy",
            "dd MMM yyyy", "d MMM yyyy", "dd MMM yy", "yyyy-MM-dd", "MM/dd/yyyy", "dd.MM.yyyy", "dd/MM/yyyy HH:mm:ss", "yyyy-MM-dd HH:mm:ss"
        };

        static StatementParser()
        {
            try { Encoding.RegisterProvider(CodePagesEncodingProvider.Instance); } catch { }
        }

        public static List<Transaction> Parse(Stream stream, string fileName)
        {
            var ext = Path.GetExtension(fileName).ToLowerInvariant();
            DataTable table;
            if (ext == ".csv" || ext == ".txt")
            {
                using var sr = new StreamReader(stream);
                table = ParseCsv(sr.ReadToEnd());
            }
            else
            {
                using var reader = ExcelReaderFactory.CreateReader(stream);
                table = reader.AsDataSet().Tables[0];
            }
            return ParseTable(table);
        }

        private static List<Transaction> ParseTable(DataTable table)
        {
            int headerRow = -1, dateCol = -1, descCol = -1, debitCol = -1, creditCol = -1, amountCol = -1, typeCol = -1;

            for (int r = 0; r < Math.Min(table.Rows.Count, 60) && headerRow < 0; r++)
            {
                var cells = Enumerable.Range(0, table.Columns.Count).Select(c => Norm(table.Rows[r][c])).ToArray();
                int d = Find(cells, DateHeaders), n = Find(cells, DescHeaders);
                if (d < 0 || n < 0) continue;

                headerRow = r; dateCol = d; descCol = n;
                debitCol = Find(cells, DebitHeaders);
                creditCol = Find(cells, CreditHeaders);
                amountCol = Find(cells, AmountHeaders);
                typeCol = Find(cells, TypeHeaders);
            }

            if (headerRow < 0)
                throw new InvalidDataException("Couldn't find a header row with Date and Description/Narration columns.");
            if (debitCol < 0 && creditCol < 0 && amountCol < 0)
                throw new InvalidDataException("Couldn't find Debit/Credit or Amount columns.");

            var result = new List<Transaction>();
            for (int r = headerRow + 1; r < table.Rows.Count; r++)
            {
                var row = table.Rows[r];
                if (!TryDate(row[dateCol], out var date)) continue;
                var desc = Clean(row[descCol]?.ToString());
                if (string.IsNullOrWhiteSpace(desc)) continue;

                decimal debit = debitCol >= 0 ? Money(row[debitCol]) : 0;
                decimal credit = creditCol >= 0 ? Money(row[creditCol]) : 0;

                if (debit == 0 && credit == 0 && amountCol >= 0)
                {
                    var raw = row[amountCol]?.ToString() ?? "";
                    var amt = Money(row[amountCol]);
                    var typeText = typeCol >= 0 ? Norm(row[typeCol]) : "";
                    bool isCredit = typeText.StartsWith("cr") || typeText.Contains("credit") || raw.Trim().EndsWith("Cr", StringComparison.OrdinalIgnoreCase);
                    bool isDebit = typeText.StartsWith("dr") || typeText.Contains("debit") || raw.Trim().EndsWith("Dr", StringComparison.OrdinalIgnoreCase);
                    if (isCredit) credit = Math.Abs(amt);
                    else if (isDebit) debit = Math.Abs(amt);
                    else if (amt < 0) debit = -amt;
                    else debit = amt; // card statements list spends as positive numbers
                }

                if (debit == 0 && credit == 0) continue;
                bool income = credit > 0 && debit == 0;
                var kind = Categories.GuessKind(desc, income);

                result.Add(new Transaction
                {
                    TxnDate = date,
                    Description = desc.Length > 200 ? desc[..200] : desc,
                    Amount = income ? credit : debit,
                    TxnType = kind,
                    Category = kind == "Investment" ? Categories.AutoCategorizeInvestment(desc) : Categories.AutoCategorize(desc, income),
                    PaymentMode = GuessMode(desc)
                });
            }
            return result;
        }

        public static string GuessMode(string desc)
        {
            var d = desc.ToLowerInvariant();
            if (d.Contains("upi")) return "UPI";
            if (d.Contains("atm") || d.Contains("cash")) return "Cash";
            if (d.Contains("pos") || d.Contains("debit card")) return "Debit Card";
            if (d.Contains("nach") || d.Contains("ach") || d.Contains("si-") || d.Contains("auto")) return "Auto-debit";
            if (d.Contains("neft") || d.Contains("imps") || d.Contains("rtgs") || d.Contains("netbank")) return "Net Banking";
            return "UPI";
        }

        private static int Find(string[] cells, string[] candidates)
        {
            foreach (var c in candidates)
            {
                int i = Array.FindIndex(cells, x => x == c);
                if (i >= 0) return i;
            }
            foreach (var c in candidates.Where(c => c.Length > 3))
            {
                int i = Array.FindIndex(cells, x => x.StartsWith(c));
                if (i >= 0) return i;
            }
            return -1;
        }

        private static string Norm(object? o) =>
            (o?.ToString() ?? "").Replace('\u00A0', ' ').Replace(".", "").Trim().ToLowerInvariant();

        private static string Clean(string? s) =>
            string.Join(' ', (s ?? "").Replace('\u00A0', ' ').Split(new[] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries));

        private static bool TryDate(object? o, out DateTime date)
        {
            date = default;
            if (o is DateTime dt) { date = dt.Date; return true; }
            if (o is double oa && oa > 20000 && oa < 80000) { date = DateTime.FromOADate(oa).Date; return true; }
            var s = o?.ToString()?.Trim();
            if (string.IsNullOrEmpty(s)) return false;
            if (DateTime.TryParseExact(s, DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out date)) { date = date.Date; return true; }
            if (DateTime.TryParse(s, new CultureInfo("en-IN"), DateTimeStyles.AllowWhiteSpaces, out date)) { date = date.Date; return true; }
            return false;
        }

        private static decimal Money(object? o)
        {
            if (o is double d) return (decimal)d;
            if (o is decimal m) return m;
            var s = (o?.ToString() ?? "").Replace(",", "").Replace("\u20B9", "").Replace("INR", "", StringComparison.OrdinalIgnoreCase)
                .Replace("Cr", "", StringComparison.OrdinalIgnoreCase).Replace("Dr", "", StringComparison.OrdinalIgnoreCase).Trim();
            bool neg = s.StartsWith("(") && s.EndsWith(")");
            s = s.Trim('(', ')');
            return decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? (neg ? -v : v) : 0;
        }

        private static DataTable ParseCsv(string text)
        {
            var table = new DataTable();
            var rows = new List<List<string>>();
            var row = new List<string>();
            var field = new StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (inQuotes)
                {
                    if (c == '"' && i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i++; }
                    else if (c == '"') inQuotes = false;
                    else field.Append(c);
                }
                else if (c == '"') inQuotes = true;
                else if (c == ',') { row.Add(field.ToString()); field.Clear(); }
                else if (c == '\n' || c == '\r')
                {
                    if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                    row.Add(field.ToString()); field.Clear();
                    rows.Add(row); row = new List<string>();
                }
                else field.Append(c);
            }
            if (field.Length > 0 || row.Count > 0) { row.Add(field.ToString()); rows.Add(row); }

            int cols = rows.Count == 0 ? 0 : rows.Max(r => r.Count);
            for (int c = 0; c < cols; c++) table.Columns.Add($"C{c}", typeof(string));
            foreach (var r in rows)
            {
                var dr = table.NewRow();
                for (int c = 0; c < r.Count; c++) dr[c] = r[c];
                table.Rows.Add(dr);
            }
            return table;
        }

        public static string ToCsv(IEnumerable<Transaction> txns, Func<string?, string?> accountName)
        {
            static string Esc(string? s) => s == null ? "" : (s.Contains(',') || s.Contains('"') || s.Contains('\n') ? $"\"{s.Replace("\"", "\"\"")}\"" : s);
            var sb = new StringBuilder();
            sb.AppendLine("Date,Description,Type,Category,Amount,Payment Mode,Account,Notes");
            foreach (var t in txns.OrderBy(t => t.TxnDate))
            {
                sb.AppendLine(string.Join(',',
                    t.TxnDate.ToString("yyyy-MM-dd"), Esc(t.Description), t.TxnType, Esc(t.Category),
                    (t.IsIncome ? t.Amount : -t.Amount).ToString(System.Globalization.CultureInfo.InvariantCulture),
                    Esc(t.PaymentMode), Esc(accountName(t.AccountId)), Esc(t.Notes)));
            }
            return sb.ToString();
        }
    }
}
