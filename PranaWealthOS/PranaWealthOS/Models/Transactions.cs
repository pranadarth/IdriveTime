using Postgrest.Attributes;
using Postgrest.Models;

namespace PranaWealthOS.Models
{
    [Table("transactions")]
    public class Transaction : BaseModel
    {
        [PrimaryKey("id", false)]
        public string Id { get; set; }

        [Column("user_id")]
        public string UserId { get; set; }

        [Column("txn_date")]
        public DateTime TxnDate { get; set; } = DateTime.Today;

        [Column("description")]
        public string Description { get; set; } = "";

        // Always stored as a positive number; TxnType decides direction
        [Column("amount")]
        public decimal Amount { get; set; }

        [Column("txn_type")]
        public string TxnType { get; set; } = "Expense"; // Expense | Income

        [Column("category")]
        public string Category { get; set; } = "Other";

        [Column("payment_mode")]
        public string PaymentMode { get; set; } = "UPI";

        [Column("account_id")]
        public string? AccountId { get; set; }

        [Column("notes")]
        public string? Notes { get; set; }

        [Newtonsoft.Json.JsonIgnore]
        public bool IsExpense => TxnType != "Income";
    }

    [Table("budgets")]
    public class Budget : BaseModel
    {
        [PrimaryKey("id", false)]
        public string Id { get; set; }

        [Column("user_id")]
        public string UserId { get; set; }

        [Column("category")]
        public string Category { get; set; } = "";

        [Column("monthly_limit")]
        public decimal MonthlyLimit { get; set; }
    }

    public record CategoryInfo(string Name, string Icon, string Color);

    public static class Categories
    {
        public static readonly List<CategoryInfo> Expense = new()
        {
            new("Food & Dining", MudBlazor.Icons.Material.Rounded.Restaurant, "#f97316"),
            new("Groceries", MudBlazor.Icons.Material.Rounded.ShoppingCart, "#22c55e"),
            new("Transport", MudBlazor.Icons.Material.Rounded.DirectionsCar, "#3b82f6"),
            new("Shopping", MudBlazor.Icons.Material.Rounded.ShoppingBag, "#ec4899"),
            new("Bills & Utilities", MudBlazor.Icons.Material.Rounded.Bolt, "#eab308"),
            new("Housing", MudBlazor.Icons.Material.Rounded.Home, "#8b5cf6"),
            new("Entertainment", MudBlazor.Icons.Material.Rounded.Movie, "#ef4444"),
            new("Health", MudBlazor.Icons.Material.Rounded.LocalHospital, "#14b8a6"),
            new("Education", MudBlazor.Icons.Material.Rounded.School, "#0ea5e9"),
            new("Travel", MudBlazor.Icons.Material.Rounded.Flight, "#06b6d4"),
            new("Insurance", MudBlazor.Icons.Material.Rounded.Shield, "#64748b"),
            new("EMI & Loans", MudBlazor.Icons.Material.Rounded.AccountBalance, "#dc2626"),
            new("Personal Care", MudBlazor.Icons.Material.Rounded.Spa, "#d946ef"),
            new("Gifts & Donations", MudBlazor.Icons.Material.Rounded.CardGiftcard, "#f43f5e"),
            new("Investments", MudBlazor.Icons.Material.Rounded.ShowChart, "#10b981"),
            new("Transfers", MudBlazor.Icons.Material.Rounded.SwapHoriz, "#94a3b8"),
            new("Cash", MudBlazor.Icons.Material.Rounded.Payments, "#84cc16"),
            new("Other", MudBlazor.Icons.Material.Rounded.Category, "#6b7280"),
        };

        public static readonly List<CategoryInfo> Income = new()
        {
            new("Salary", MudBlazor.Icons.Material.Rounded.Work, "#10b981"),
            new("Freelance", MudBlazor.Icons.Material.Rounded.Laptop, "#22c55e"),
            new("Interest & Dividends", MudBlazor.Icons.Material.Rounded.Savings, "#16a34a"),
            new("Refund", MudBlazor.Icons.Material.Rounded.Replay, "#4ade80"),
            new("Other Income", MudBlazor.Icons.Material.Rounded.AttachMoney, "#15803d"),
        };

        public static readonly string[] PaymentModes = { "UPI", "Credit Card", "Debit Card", "Net Banking", "Cash", "Auto-debit", "Wallet" };

        public static CategoryInfo Get(string? name) =>
            Expense.Concat(Income).FirstOrDefault(c => c.Name == name) ?? Expense.Last();

        private static readonly (string Category, string[] Keywords)[] Rules =
        {
            ("Salary", new[] { "salary", "sal cr", "payroll" }),
            ("Interest & Dividends", new[] { "int.pd", "interest", "dividend", "int pd" }),
            ("Refund", new[] { "refund", "reversal", "cashback" }),
            ("Food & Dining", new[] { "swiggy", "zomato", "restaurant", "cafe", "domino", "mcdonald", "kfc", "starbucks", "pizza", "burger", "eatsure", "dining", "chaayos" }),
            ("Groceries", new[] { "blinkit", "zepto", "bigbasket", "dmart", "instamart", "grofers", "jiomart", "more retail", "reliance fresh", "supermarket", "kirana" }),
            ("Transport", new[] { "uber", "ola", "rapido", "fuel", "petrol", "hpcl", "bpcl", "iocl", "indian oil", "shell", "metro", "fastag", "parking", "namma yatri" }),
            ("Travel", new[] { "irctc", "makemytrip", "goibibo", "cleartrip", "indigo", "air india", "vistara", "akasa", "oyo", "airbnb", "hotel", "redbus", "ixigo", "yatra" }),
            ("Shopping", new[] { "amazon", "flipkart", "myntra", "ajio", "meesho", "nykaa", "croma", "reliance digital", "decathlon", "ikea", "tata cliq", "lenskart" }),
            ("Bills & Utilities", new[] { "electricity", "bescom", "tneb", "msedcl", "tata power", "adani elec", "airtel", "jio", "vodafone", "vi prepaid", "broadband", "act fibernet", "recharge", "gas", "water bill", "bill pay", "billdesk", "dth", "tata play" }),
            ("Entertainment", new[] { "netflix", "spotify", "hotstar", "prime video", "youtube", "bookmyshow", "pvr", "inox", "sonyliv", "zee5", "jiocinema", "steam", "playstation" }),
            ("Health", new[] { "pharmacy", "apollo", "medplus", "hospital", "clinic", "1mg", "pharmeasy", "practo", "netmeds", "diagnostic", "lab" }),
            ("Housing", new[] { "rent", "nobroker", "maintenance", "society", "nestaway", "housing.com" }),
            ("Education", new[] { "school", "college", "udemy", "coursera", "tuition", "byju", "unacademy", "fees" }),
            ("Insurance", new[] { "lic", "insurance", "policybazaar", "hdfc ergo", "star health", "icici lombard", "acko", "digit" }),
            ("EMI & Loans", new[] { "emi", "loan", "bajaj fin", "home credit", "nach" }),
            ("Investments", new[] { "zerodha", "groww", "upstox", "kuvera", "mutual fund", "sip", "nps", "ppf", "coin by", "indmoney", "smallcase" }),
            ("Personal Care", new[] { "salon", "urban company", "urbanclap", "spa", "gym", "cult.fit", "cultfit" }),
            ("Cash", new[] { "atm", "cash wdl", "cash withdrawal", "nwd" }),
            ("Transfers", new[] { "self transfer", "own account", "transfer to", "neft", "imps", "rtgs" }),
        };

        public static string AutoCategorize(string? description, bool isIncome = false)
        {
            var d = (description ?? "").ToLowerInvariant();
            foreach (var (category, keywords) in Rules)
            {
                bool isIncomeCat = Income.Any(c => c.Name == category);
                if (isIncomeCat != isIncome && category != "Transfers") continue;
                if (keywords.Any(k => d.Contains(k))) return category;
            }
            return isIncome ? "Other Income" : "Other";
        }
    }
}
