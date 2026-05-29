using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using ExcelDataReader; 

namespace WealthTrackerOS
{
    // These classes remain the same as before
    public class StockHolding
    {
        public string StockName { get; set; }
        public string ISIN { get; set; }
        public decimal Quantity { get; set; }
        public decimal BuyValue { get; set; }
        public decimal ClosingValue { get; set; }
    }

    public class MutualFundHolding
    {
        public string SchemeName { get; set; }
        public decimal InvestedValue { get; set; }
        public decimal CurrentValue { get; set; }

        // We add this to separate your buckets
        public bool IsShortTermGoal { get; set; }
    }

    class Program
    {
        static void Main(string[] args)
        {
            // Register encoding for ExcelDataReader (Crucial for modern .NET apps)
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
            Console.OutputEncoding = Encoding.UTF8;

            // Updated paths with .xlsx extension
            string stockPath = @"C:\Users\pranadarth\Downloads\FinData\Stocks_Holdings_Statement_4084575549_2026-05-26.xlsx";
            string mfPath = @"C:\Users\pranadarth\Downloads\FinData\Holdings_Statement_2026-05-27.xlsx";

            Console.WriteLine("--- Pranav's Wealth OS: Booting Up (Excel Edition) ---\n");

            try
            {
                // 1. Process Stocks (Pure Equity)
                var stocks = ParseExcelStocks(stockPath)
                             .Where(s => s.StockName != "null" && s.ISIN != "INE342T07577").ToList();

                // 2. Process Mutual Funds & Apply Bucketing
                var allMfs = ParseExcelMFs(mfPath);

                // Separate the Tata Ultra Short Term Fund
                var bondGoalSavings = allMfs.Where(m => m.SchemeName.Contains("Tata Ultra Short Term")).ToList();
                var longTermEquityMfs = allMfs.Where(m => !m.SchemeName.Contains("Tata Ultra Short Term")).ToList();

                // 3. Calculated Totals
                decimal totalEquityValue = stocks.Sum(s => s.ClosingValue) + longTermEquityMfs.Sum(m => m.CurrentValue);
                decimal totalBondGoalValue = bondGoalSavings.Sum(m => m.CurrentValue);

                Console.WriteLine("--- Pranav's Wealth OS: Dashboard ---\n");

                Console.WriteLine($"[EQUITY BUCKET]");
                Console.WriteLine($"- Stocks: ₹{stocks.Sum(s => s.ClosingValue):N2}");
                Console.WriteLine($"- Equity MFs: ₹{longTermEquityMfs.Sum(m => m.CurrentValue):N2}");
                Console.WriteLine($"TOTAL EQUITY WEALTH: ₹{totalEquityValue:N2}");

                Console.WriteLine($"\n[BOND GOAL BUCKET]");
                Console.WriteLine($"- Savings (Tata Fund): ₹{totalBondGoalValue:N2}");
                Console.WriteLine($"PROGRESS: {(totalBondGoalValue / 110000) * 100:N1}% of ₹1.1L Goal");

                Console.WriteLine($"\n--> GRAND TOTAL NET WORTH: ₹{totalEquityValue + totalBondGoalValue:N2} <--");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"CRITICAL ERROR: {ex.Message}");
            }

            Console.WriteLine("\nPress Enter to exit...");
            Console.ReadLine();
        }

        // --- EXCEL PARSERS ---

        static List<StockHolding> ParseExcelStocks(string filePath)
        {
            var list = new List<StockHolding>();
            using (var stream = File.Open(filePath, FileMode.Open, FileAccess.Read))
            using (var reader = ExcelReaderFactory.CreateReader(stream))
            {
                var result = reader.AsDataSet(); // Read into a DataSet
                var table = result.Tables[0];   // Assume first sheet
                bool startReading = false;

                foreach (DataRow row in table.Rows)
                {
                    // Look for the row that starts the table
                    if (row[0]?.ToString() == "Stock Name") { startReading = true; continue; }
                    if (!startReading) continue;
                    if (string.IsNullOrWhiteSpace(row[0]?.ToString())) break; // End of table

                    list.Add(new StockHolding
                    {
                        StockName = row[0]?.ToString(),
                        ISIN = row[1]?.ToString(),
                        Quantity = Convert.ToDecimal(row[2]),
                        BuyValue = Convert.ToDecimal(row[4]),
                        ClosingValue = Convert.ToDecimal(row[6])
                    });
                }
            }
            return list;
        }

        static List<MutualFundHolding> ParseExcelMFs(string filePath)
        {
            var list = new List<MutualFundHolding>();
            using (var stream = File.Open(filePath, FileMode.Open, FileAccess.Read))
            using (var reader = ExcelReaderFactory.CreateReader(stream))
            {
                var result = reader.AsDataSet();
                var table = result.Tables[0];
                bool startReading = false;

                foreach (DataRow row in table.Rows)
                {
                    string firstCell = row[0]?.ToString()?.Trim();

                    // 1. Find the header
                    if (firstCell == "Scheme Name") { startReading = true; continue; }
                    if (!startReading) continue;

                    // 2. Skip empty rows (Groww often leaves one blank row after the header)
                    if (string.IsNullOrWhiteSpace(firstCell)) continue;

                    // 3. Stop if we reach a summary or footer row
                    if (firstCell.Contains("Total") || firstCell.Contains("Disclaimer")) break;

                    try
                    {
                        list.Add(new MutualFundHolding
                        {
                            SchemeName = firstCell,
                            // Column 7 is Invested, Column 8 is Current Value
                            InvestedValue = Convert.ToDecimal(row[7]),
                            CurrentValue = Convert.ToDecimal(row[8])
                        });
                    }
                    catch
                    {
                        // If a row fails to convert (e.g. a sub-header), just skip it
                        continue;
                    }
                }
            }
            return list;
        }
    }
}