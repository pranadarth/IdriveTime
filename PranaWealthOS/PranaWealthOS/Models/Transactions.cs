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
        public string TxnType { get; set; } = "Expense"; // Expense | Income | Investment

        [Column("category")]
        public string Category { get; set; } = "Other";

        [Column("payment_mode")]
        public string PaymentMode { get; set; } = "UPI";

        [Column("account_id")]
        public string? AccountId { get; set; }

        [Column("notes")]
        public string? Notes { get; set; }

        // Links an entry to the recurring rule it fulfils (overrides or skips that month's auto entry)
        [Column("recurring_id")]
        public string? RecurringId { get; set; }

        [Newtonsoft.Json.JsonIgnore]
        public bool IsExpense => TxnType == "Expense";

        [Newtonsoft.Json.JsonIgnore]
        public bool IsIncome => TxnType == "Income";

        [Newtonsoft.Json.JsonIgnore]
        public bool IsInvestment => TxnType == "Investment";

        // Counts towards "spent" (transfers between own accounts are excluded)
        [Newtonsoft.Json.JsonIgnore]
        public bool IsSpend => IsExpense && Category != "Transfers";

        // Not persisted: true for entries generated from a recurring rule and not yet saved
        [Newtonsoft.Json.JsonIgnore]
        public bool IsVirtual { get; set; }
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

        public static readonly List<CategoryInfo> Investment = new()
        {
            new("Equity SIP", MudBlazor.Icons.Material.Rounded.TrendingUp, "#6366f1"),
            new("Index Fund", MudBlazor.Icons.Material.Rounded.StackedLineChart, "#4f46e5"),
            new("Stocks", MudBlazor.Icons.Material.Rounded.CandlestickChart, "#7c3aed"),
            new("NPS", MudBlazor.Icons.Material.Rounded.Elderly, "#0891b2"),
            new("PPF", MudBlazor.Icons.Material.Rounded.Lock, "#0d9488"),
            new("Debt Fund", MudBlazor.Icons.Material.Rounded.Savings, "#0284c7"),
            new("Gold", MudBlazor.Icons.Material.Rounded.Diamond, "#ca8a04"),
            new("RD / FD", MudBlazor.Icons.Material.Rounded.AccountBalance, "#059669"),
            new("Other", MudBlazor.Icons.Material.Rounded.Category, "#6b7280"),
        };

        public static List<CategoryInfo> For(string kind) => kind switch
        {
            "Income" => Income,
            "Investment" => Investment,
            _ => Expense
        };

        public static string DefaultFor(string kind) => kind switch
        {
            "Income" => "Salary",
            "Investment" => "Equity SIP",
            _ => "Food & Dining"
        };

        public static readonly string[] PaymentModes = { "UPI", "Credit Card", "Debit Card", "Net Banking", "Cash", "Auto-debit", "Wallet" };

        public static CategoryInfo Get(string? name) =>
            Expense.Concat(Income).Concat(Investment).FirstOrDefault(c => c.Name == name) ?? Expense.Last();

        // Kind inferred from description for imported rows
        public static string GuessKind(string? description, bool isCredit)
        {
            if (isCredit) return "Income";
            var d = (description ?? "").ToLowerInvariant();
            string[] inv = { "zerodha", "groww", "upstox", "kuvera", "mutual fund", "sip", "nps", "ppf", "coin by", "indmoney", "smallcase", "bse star", "nse clearing", "iccl", "cams", "kfintech" };
            return inv.Any(d.Contains) ? "Investment" : "Expense";
        }

        public static string AutoCategorizeInvestment(string? description)
        {
            var d = (description ?? "").ToLowerInvariant();
            if (d.Contains("nps")) return "NPS";
            if (d.Contains("ppf")) return "PPF";
            if (d.Contains("gold") || d.Contains("sgb")) return "Gold";
            if (d.Contains("index") || d.Contains("nifty")) return "Index Fund";
            if (d.Contains("zerodha") || d.Contains("upstox") || d.Contains("smallcase")) return "Stocks";
            return "Equity SIP";
        }

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
