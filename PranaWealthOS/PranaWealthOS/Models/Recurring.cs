using Newtonsoft.Json;
using Postgrest.Attributes;
using Postgrest.Models;

namespace PranaWealthOS.Models
{
    // A rule that repeats (rent, salary, SIP...). It keeps applying every period until it is changed or stopped.
    [Table("recurring_items")]
    public class RecurringItem : BaseModel
    {
        [PrimaryKey("id", false)]
        public string Id { get; set; }

        [Column("user_id")]
        public string UserId { get; set; }

        [Column("name")]
        public string Name { get; set; } = "";

        [Column("amount")]
        public decimal Amount { get; set; }

        [Column("kind")]
        public string Kind { get; set; } = TxnKinds.Expense; // Expense | Income | Investment

        [Column("category")]
        public string Category { get; set; } = "Other";

        [Column("frequency")]
        public string Frequency { get; set; } = "Monthly"; // Monthly | Quarterly | Half-yearly | Yearly

        [Column("day_of_month")]
        public int DayOfMonth { get; set; } = 1;

        [Column("start_month")]
        public DateTime StartMonth { get; set; } = new(DateTime.Today.Year, DateTime.Today.Month, 1);

        [Column("end_month")]
        public DateTime? EndMonth { get; set; }

        [Column("is_essential")]
        public bool IsEssential { get; set; } = true;

        [Column("payment_mode")]
        public string PaymentMode { get; set; } = "Auto-debit";

        [Column("account_id")]
        public string? AccountId { get; set; }

        public static readonly string[] Frequencies = { "Monthly", "Quarterly", "Half-yearly", "Yearly" };

        [JsonIgnore]
        public int IntervalMonths => Frequency switch { "Quarterly" => 3, "Half-yearly" => 6, "Yearly" => 12, _ => 1 };

        [JsonIgnore]
        public decimal MonthlyEquivalent => Amount / IntervalMonths;

        private static DateTime First(DateTime d) => new(d.Year, d.Month, 1);

        public bool IsActiveIn(DateTime month)
        {
            var m = First(month);
            return First(StartMonth) <= m && (EndMonth == null || First(EndMonth.Value) >= m);
        }

        public bool OccursIn(DateTime month)
        {
            var m = First(month);
            if (!IsActiveIn(m)) return false;
            var s = First(StartMonth);
            int diff = (m.Year - s.Year) * 12 + m.Month - s.Month;
            return diff % IntervalMonths == 0;
        }

        public DateTime DateIn(DateTime month) =>
            new(month.Year, month.Month, Math.Clamp(DayOfMonth, 1, DateTime.DaysInMonth(month.Year, month.Month)));

        public DateTime? NextDate(DateTime from)
        {
            var m = First(from);
            for (int i = 0; i < 13; i++, m = m.AddMonths(1))
            {
                if (!OccursIn(m)) continue;
                var d = DateIn(m);
                if (d >= from.Date) return d;
            }
            return null;
        }
    }

    public static class TxnKinds
    {
        public const string Expense = "Expense";
        public const string Income = "Income";
        public const string Investment = "Investment";
        public static readonly string[] All = { Expense, Income, Investment };
    }
}
