using System.Globalization;

namespace PranaWealthOS.Services
{
    // Central formatting. Non-ASCII glyphs are written as escapes so source encoding can never corrupt them.
    public static class Fmt
    {
        public const string Rupee = "\u20B9";
        public const string Dot = "\u00B7";
        private static readonly CultureInfo In = new("en-IN");

        public static string Inr(decimal v) => (v < 0 ? "-" : "") + Rupee + Math.Abs(Math.Round(v)).ToString("N0", In);

        public static string Signed(decimal v) => (v > 0 ? "+" : v < 0 ? "-" : "") + Rupee + Math.Abs(Math.Round(v)).ToString("N0", In);

        // 1,23,45,678 -> 1.23 Cr ; 4,50,000 -> 4.5 L
        public static string Compact(decimal v)
        {
            var a = Math.Abs(v);
            var sign = v < 0 ? "-" : "";
            if (a >= 1_00_00_000m) return $"{sign}{Rupee}{(a / 1_00_00_000m).ToString("0.##", In)} Cr";
            if (a >= 1_00_000m) return $"{sign}{Rupee}{(a / 1_00_000m).ToString("0.##", In)} L";
            if (a >= 1_000m) return $"{sign}{Rupee}{(a / 1_000m).ToString("0.#", In)}K";
            return $"{sign}{Rupee}{a.ToString("N0", In)}";
        }

        public static string Pct(double v, int decimals = 0) => double.IsFinite(v) ? v.ToString("F" + decimals, In) + "%" : "-";

        public static double Ratio(decimal part, decimal whole) => whole == 0 ? 0 : (double)(part / whole * 100);

        public static string Css(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);

        public static DateTime MonthOf(DateTime d) => new(d.Year, d.Month, 1);

        public static DateTime ThisMonth => MonthOf(DateTime.Today);

        public static string Greeting()
        {
            var h = DateTime.Now.Hour;
            return h < 12 ? "Good morning" : h < 17 ? "Good afternoon" : "Good evening";
        }
    }
}
