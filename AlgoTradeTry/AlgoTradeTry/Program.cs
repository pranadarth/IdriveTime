using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using YahooFinanceApi;

namespace YahooFinanceAlgoTrading
{
    // Represents a single price data point
    public class PriceData
    {
        public DateTime Date { get; set; }
        public decimal Close { get; set; }
    }

    class Program
    {
        static async Task Main(string[] args)
        {
            // Set the stock symbol (for Indian stocks, add ".NS"; here, TCS)
            var symbol = "TCS.NS";
            // Define the time range for historical data (last 3 months)
            var startDate = DateTime.Now.AddMonths(-3);
            var endDate = DateTime.Now;

            Console.WriteLine("Fetching historical data from Yahoo Finance...");
            var historicalData = await Yahoo.GetHistoricalAsync(symbol, startDate, endDate, Period.Daily);

            // Convert Yahoo data to our PriceData list
            var priceDataList = historicalData
                .Select(x => new PriceData { Date = x.DateTime, Close = x.Close })
                .OrderBy(x => x.Date)
                .ToList();

            Console.WriteLine($"Fetched {priceDataList.Count} data points for {symbol}.");

            // Define moving average windows (example: 20-day and 50-day)
            int shortWindow = 20;
            int longWindow = 50;

            // Calculate trading signals based on a simple moving average crossover strategy
            var signals = CalculateSignals(priceDataList, shortWindow, longWindow);
            Console.WriteLine("\nDate\t\tSignal");
            foreach (var signal in signals)
            {
                string action = signal.Signal == 1 ? "Buy" : signal.Signal == -1 ? "Sell" : "Neutral";
                Console.WriteLine($"{signal.Date.ToShortDateString()}\t{action}");
            }

            Console.WriteLine("\nPress any key to exit...");
            Console.ReadKey();
        }

        // Calculate trading signals using moving averages
        public static List<(DateTime Date, int Signal)> CalculateSignals(List<PriceData> data, int shortWindow, int longWindow)
        {
            var signals = new List<(DateTime Date, int Signal)>();
            var shortMA = CalculateMovingAverage(data, shortWindow);
            var longMA = CalculateMovingAverage(data, longWindow);

            for (int i = 0; i < data.Count; i++)
            {
                int signal = 0;
                if (i >= longWindow - 1)
                {
                    if (shortMA[i] > longMA[i])
                        signal = 1;  // Buy signal
                    else if (shortMA[i] < longMA[i])
                        signal = -1; // Sell signal
                }
                signals.Add((data[i].Date, signal));
            }
            return signals;
        }

        // Helper method to calculate moving averages
        public static List<decimal> CalculateMovingAverage(List<PriceData> data, int window)
        {
            var movingAverages = new List<decimal>();
            for (int i = 0; i < data.Count; i++)
            {
                if (i < window - 1)
                {
                    decimal avg = data.Take(i + 1).Average(x => x.Close);
                    movingAverages.Add(avg);
                }
                else
                {
                    decimal avg = data.Skip(i - window + 1).Take(window).Average(x => x.Close);
                    movingAverages.Add(avg);
                }
            }
            return movingAverages;
        }
    }
}
