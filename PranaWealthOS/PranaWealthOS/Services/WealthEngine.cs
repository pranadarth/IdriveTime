using PranaWealthOS.Models;
using static Postgrest.Constants;

namespace PranaWealthOS.Services
{
    // Everything a page needs, loaded in parallel once and cached for the session.
    public class FinanceSnapshot
    {
        public UserSettings Settings { get; init; } = new();
        public List<AssetModel> Assets { get; init; } = new();
        public List<RecurringItem> Recurring { get; init; } = new();
        public List<BankAccount> Accounts { get; init; } = new();
        public List<Subscription> Subscriptions { get; init; } = new();
        public List<Budget> Budgets { get; init; } = new();
        public List<Transaction> RecentTxns { get; init; } = new(); // last ~4 months
        public DateTime LoadedAt { get; init; } = DateTime.UtcNow;

        // ---------- Assets ----------
        private IEnumerable<AssetModel> Of(string type) => Assets.Where(a => a.AssetType == type);
        public decimal StocksCore => Of("Stock").Where(a => !a.IsShortTermGoal).Sum(a => a.CurrentValue);
        public decimal MfCore => Of("MF").Where(a => !a.IsShortTermGoal).Sum(a => a.CurrentValue);
        public decimal EquityCore => StocksCore + MfCore;
        public decimal EquityInvested => Of("Stock").Concat(Of("MF")).Where(a => !a.IsShortTermGoal).Sum(a => a.InvestedValue);
        public decimal ShortTermFunds => Of("Stock").Concat(Of("MF")).Where(a => a.IsShortTermGoal).Sum(a => a.CurrentValue);
        public AssetModel? Epf => Of("EPF").FirstOrDefault();
        public decimal EpfValue => Epf?.CurrentValue ?? 0;
        public decimal RdValue => Of("RD").Sum(a => a.CurrentValue);
        public decimal FdValue => Of("FD").Where(a => !a.IsBond).Sum(a => a.InvestedValue);
        public decimal BondValue => Of("FD").Where(a => a.IsBond).Sum(a => a.InvestedValue);
        public decimal DebtTotal => EpfValue + RdValue + FdValue + BondValue;
        public decimal Physical => Of("Physical").Sum(a => a.CurrentValue);
        public decimal CashInBank => Accounts.Where(a => !a.IsCredit).Sum(a => a.Balance);
        public decimal CardDues => Accounts.Where(a => a.IsCredit).Sum(a => a.Balance);

        // Investable net worth used for FIRE (excludes short-term earmarked funds, as before)
        public decimal CoreNetWorth => EquityCore + DebtTotal + Physical;
        public decimal TotalNetWorth => CoreNetWorth + ShortTermFunds + CashInBank - CardDues;

        // ---------- Recurring ----------
        public IEnumerable<RecurringItem> ActiveIn(DateTime month, string kind) =>
            Recurring.Where(r => r.Kind == kind && r.IsActiveIn(month));

        public decimal RecurringMonthly(string kind, DateTime month, bool essentialOnly = false) =>
            ActiveIn(month, kind).Where(r => !essentialOnly || r.IsEssential).Sum(r => r.MonthlyEquivalent);

        public decimal SubscriptionsMonthly => Subscriptions.Where(s => s.IsActive).Sum(s => s.MonthlyCost);

        // Contributions that live on the asset itself (EPF, RD)
        public decimal EpfMonthly => Epf?.MonthlyContribution ?? 0;
        public decimal RdMonthly => Of("RD").Where(a => a.MonthsDuration > 0).Sum(a => a.MonthlyContribution);

        public decimal EquitySipMonthly(DateTime month) =>
            ActiveIn(month, TxnKinds.Investment).Where(r => InvestmentCategories.IsEquity(r.Category)).Sum(r => r.MonthlyEquivalent);

        public decimal OtherInvestMonthly(DateTime month) =>
            ActiveIn(month, TxnKinds.Investment).Where(r => !InvestmentCategories.IsEquity(r.Category)).Sum(r => r.MonthlyEquivalent);

        public decimal TotalInvestMonthly(DateTime month) => EquitySipMonthly(month) + OtherInvestMonthly(month) + EpfMonthly + RdMonthly;

        public decimal IncomeMonthly(DateTime month)
        {
            var rec = RecurringMonthly(TxnKinds.Income, month);
            return rec > 0 ? rec : Settings.MonthlyIncome;
        }

        // Average one-off (non-recurring) spending of the last 3 completed months
        public decimal VariableSpendAvg
        {
            get
            {
                var start = Fmt.ThisMonth.AddMonths(-3);
                var months = Enumerable.Range(0, 3).Select(i => start.AddMonths(i))
                    .Select(m => RecentTxns.Where(t => t.IsExpense && t.Category != "Transfers" && Fmt.MonthOf(t.TxnDate) == m).Sum(t => t.Amount))
                    .Where(v => v > 0).ToList();
                return months.Count == 0 ? 0 : months.Average();
            }
        }

        // The single source of truth for "how much do I need per month"
        public decimal MonthlyCostOfLiving(bool essentialsOnly)
        {
            var m = Fmt.ThisMonth;
            var fixedCost = RecurringMonthly(TxnKinds.Expense, m, essentialsOnly);
            if (essentialsOnly) return fixedCost;
            return fixedCost + SubscriptionsMonthly + VariableSpendAvg;
        }

        // Actual + recurring (not yet overridden) totals for a month
        public (decimal Income, decimal Spent, decimal Invested) MonthFlow(DateTime month)
        {
            var real = RecentTxns.Where(t => Fmt.MonthOf(t.TxnDate) == month).ToList();
            var overridden = real.Where(t => !string.IsNullOrEmpty(t.RecurringId)).Select(t => t.RecurringId!).ToHashSet();
            decimal inc = real.Where(t => t.IsIncome).Sum(t => t.Amount);
            decimal spent = real.Where(t => t.IsSpend).Sum(t => t.Amount);
            decimal inv = real.Where(t => t.IsInvestment).Sum(t => t.Amount);
            foreach (var r in Recurring.Where(r => r.OccursIn(month) && !overridden.Contains(r.Id)))
            {
                if (r.Kind == TxnKinds.Income) inc += r.Amount;
                else if (r.Kind == TxnKinds.Investment) inv += r.Amount;
                else spent += r.Amount;
            }
            return (inc, spent, inv + EpfMonthly + RdMonthly);
        }

        public IEnumerable<(string Name, decimal Amount, DateTime Date, string Kind)> UpcomingBills(int days)
        {
            var today = DateTime.Today; var until = today.AddDays(days);
            var list = new List<(string, decimal, DateTime, string)>();
            foreach (var r in Recurring.Where(r => r.Kind != TxnKinds.Income))
                if (r.NextDate(today) is DateTime d && d <= until) list.Add((r.Name, r.Amount, d, r.Kind));
            foreach (var s in Subscriptions.Where(s => s.IsActive))
                if (s.UpcomingDate is DateTime d && d >= today && d <= until) list.Add((s.Name, s.Amount, d, "Subscription"));
            return list.OrderBy(x => x.Item3);
        }

        public double RealizedEquityCagr(int portfolioAgeMonths)
        {
            if (EquityInvested <= 0 || portfolioAgeMonths <= 0) return 0;
            return (Math.Pow((double)(EquityCore / EquityInvested), 12.0 / portfolioAgeMonths) - 1) * 100;
        }
    }

    public static class InvestmentCategories
    {
        public static readonly string[] All = { "Equity SIP", "Index Fund", "Stocks", "NPS", "PPF", "Debt Fund", "Gold", "RD / FD", "Other" };
        public static bool IsEquity(string? c) => c is "Equity SIP" or "Index Fund" or "Stocks" or "NPS";
    }

    public record FireInputs(
        decimal MonthlyExpense,
        decimal EquityMonthly,
        decimal DebtMonthly,
        double EquityReturn,
        double DebtReturn,
        double Inflation,
        double Swr,
        double AnnualStepUp = 0);

    public record FireYear(int Year, int? Age, decimal Portfolio, decimal Target, decimal Invested);

    public record FireResult(decimal TargetToday, int? MonthsToFire, decimal TargetAtFire, List<FireYear> Years)
    {
        public bool Reached => MonthsToFire.HasValue;
        public double YearsToFire => MonthsToFire.HasValue ? MonthsToFire.Value / 12.0 : double.NaN;
        public string Label => !MonthsToFire.HasValue ? "50+ yrs"
            : MonthsToFire.Value == 0 ? "Achieved"
            : MonthsToFire.Value < 12 ? $"{MonthsToFire} mo"
            : $"{MonthsToFire.Value / 12} yr {MonthsToFire.Value % 12} mo";
    }

    public class WealthEngine
    {
        private readonly Supabase.Client _db;
        private FinanceSnapshot? _cache;
        private string? _cacheUser;
        private Task<FinanceSnapshot>? _inflight;
        private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(2);

        public WealthEngine(Supabase.Client db) => _db = db;

        public string? UserId => _db.Auth.CurrentUser?.Id;

        public void Invalidate() => _cache = null;

        public Task<FinanceSnapshot> GetAsync(bool force = false)
        {
            var uid = UserId ?? "";
            if (!force && _cache != null && _cacheUser == uid && DateTime.UtcNow - _cache.LoadedAt < Ttl)
                return Task.FromResult(_cache);
            if (_inflight != null && !force) return _inflight;
            _inflight = LoadAsync(uid);
            return _inflight;
        }

        private async Task<FinanceSnapshot> LoadAsync(string uid)
        {
            try
            {
                if (string.IsNullOrEmpty(uid)) return new FinanceSnapshot();

                var since = Fmt.ThisMonth.AddMonths(-3).ToString("yyyy-MM-dd");
                var settingsT = Safe(() => _db.From<UserSettings>().Where(x => x.Id == uid).Get());
                var assetsT = Safe(() => _db.From<AssetModel>().Where(x => x.UserId == uid).Get());
                var recT = Safe(() => _db.From<RecurringItem>().Where(x => x.UserId == uid).Get());
                var legacyT = Safe(() => _db.From<ExpenseItem>().Where(x => x.UserId == uid).Get());
                var accT = Safe(() => _db.From<BankAccount>().Where(x => x.UserId == uid).Get());
                var subT = Safe(() => _db.From<Subscription>().Where(x => x.UserId == uid).Get());
                var budT = Safe(() => _db.From<Budget>().Where(x => x.UserId == uid).Get());
                var txT = Safe(() => _db.From<Transaction>().Where(x => x.UserId == uid).Filter("txn_date", Operator.GreaterThanOrEqual, since).Get());
                await Task.WhenAll(settingsT, assetsT, recT, legacyT, accT, subT, budT, txT);

                var settings = settingsT.Result?.FirstOrDefault() ?? new UserSettings { Id = uid };
                var recurring = recT.Result ?? new();
                var recurringTableOk = recT.Result != null;

                // One-time migration from v1: fixed expenses + Profile SIP become recurring rules
                if (recurringTableOk)
                    recurring = await MigrateLegacyAsync(uid, settings, recurring, legacyT.Result ?? new());

                _cache = new FinanceSnapshot
                {
                    Settings = settings,
                    Assets = assetsT.Result ?? new(),
                    Recurring = recurring,
                    Accounts = accT.Result ?? new(),
                    Subscriptions = subT.Result ?? new(),
                    Budgets = budT.Result ?? new(),
                    RecentTxns = txT.Result ?? new(),
                };
                _cacheUser = uid;
                return _cache;
            }
            finally { _inflight = null; }
        }

        private static async Task<List<T>?> Safe<T>(Func<Task<Postgrest.Responses.ModeledResponse<T>>> q) where T : Postgrest.Models.BaseModel, new()
        {
            try { return (await q()).Models; } catch { return null; }
        }

        private async Task<List<RecurringItem>> MigrateLegacyAsync(string uid, UserSettings settings, List<RecurringItem> recurring, List<ExpenseItem> legacy)
        {
            var toAdd = new List<RecurringItem>();
            var start = Fmt.ThisMonth;

            if (legacy.Any() && !recurring.Any(r => r.Kind == TxnKinds.Expense))
            {
                toAdd.AddRange(legacy.Where(e => e.Amount > 0).Select(e => new RecurringItem
                {
                    UserId = uid, Name = e.Name, Amount = e.Amount, Kind = TxnKinds.Expense,
                    Category = MapLegacyCategory(e.Category), IsEssential = e.IsMandatory, StartMonth = start
                }));
            }
            if (settings.MonthlySIP > 0 && !recurring.Any(r => r.Kind == TxnKinds.Investment))
            {
                toAdd.Add(new RecurringItem { UserId = uid, Name = "Monthly SIP", Amount = settings.MonthlySIP, Kind = TxnKinds.Investment, Category = "Equity SIP", StartMonth = start, IsEssential = false });
            }
            if (settings.MonthlyIncome > 0 && !recurring.Any(r => r.Kind == TxnKinds.Income))
            {
                toAdd.Add(new RecurringItem { UserId = uid, Name = "Salary", Amount = settings.MonthlyIncome, Kind = TxnKinds.Income, Category = "Salary", StartMonth = start, IsEssential = false, PaymentMode = "Net Banking" });
            }
            if (!toAdd.Any()) return recurring;

            try
            {
                var inserted = await _db.From<RecurringItem>().Insert(toAdd);
                if (legacy.Any() && toAdd.Any(t => t.Kind == TxnKinds.Expense))
                {
                    try { await _db.From<ExpenseItem>().Where(x => x.UserId == uid).Delete(); } catch { }
                }
                if (toAdd.Any(t => t.Kind != TxnKinds.Expense) && settings.Id == uid)
                {
                    // Legacy fields are now represented by recurring rules; clear them so they don't re-migrate
                    if (toAdd.Any(t => t.Kind == TxnKinds.Investment)) settings.MonthlySIP = 0;
                    if (toAdd.Any(t => t.Kind == TxnKinds.Income)) settings.MonthlyIncome = 0;
                    try { await _db.From<UserSettings>().Upsert(settings); } catch { }
                }
                return recurring.Concat(inserted.Models).ToList();
            }
            catch { return recurring; }
        }

        private static string MapLegacyCategory(string? c) => c switch
        {
            "Housing" => "Housing",
            "Food" => "Groceries",
            "Transport" => "Transport",
            "Utilities" => "Bills & Utilities",
            "Lifestyle" => "Entertainment",
            _ => "Other"
        };

        // ---------- FIRE simulation (monthly compounding, inflation-adjusted target) ----------
        public static FireResult Simulate(FinanceSnapshot s, FireInputs p, int? currentAge, int maxYears = 50)
        {
            decimal targetToday = p.Swr > 0 ? p.MonthlyExpense * 12m * (decimal)(100.0 / p.Swr) : 0;
            decimal equity = s.EquityCore;
            decimal otherDebt = 0;
            decimal physical = s.Physical;
            decimal epf = s.EpfValue;
            decimal epfMonthly = s.EpfMonthly;
            double epfRate = s.Epf?.InterestRate > 0 ? s.Epf.InterestRate : 8.25;

            var fds = s.Assets.Where(a => a.AssetType == "FD").Select(a => (Value: a.InvestedValue, Rate: a.InterestRate, Left: a.MonthsDuration)).ToList();
            var rds = s.Assets.Where(a => a.AssetType == "RD").Select(a => (Value: a.CurrentValue, Monthly: a.MonthlyContribution, Rate: a.InterestRate, Left: a.MonthsDuration)).ToList();

            decimal eqMonthly = p.EquityMonthly, debtMonthly = p.DebtMonthly;
            decimal eqRate = (decimal)(p.EquityReturn / 100 / 12);
            decimal debtRate = (decimal)(p.DebtReturn / 100 / 12);
            decimal epfR = (decimal)(epfRate / 100 / 12);
            decimal infl = (decimal)Math.Pow(1 + p.Inflation / 100, 1.0 / 12);
            decimal invested = 0;

            decimal Portfolio() => equity + otherDebt + physical + epf + fds.Sum(f => f.Value) + rds.Sum(r => r.Value);

            int? hit = targetToday > 0 && Portfolio() >= targetToday ? 0 : null;
            decimal target = targetToday;
            decimal targetAtHit = hit == 0 ? targetToday : 0;
            var years = new List<FireYear> { new(0, currentAge, Portfolio(), target, 0) };

            for (int m = 1; m <= maxYears * 12; m++)
            {
                equity = (equity + eqMonthly) * (1 + eqRate);
                otherDebt = (otherDebt + debtMonthly) * (1 + debtRate);
                epf = (epf + epfMonthly) * (1 + epfR);
                invested += eqMonthly + debtMonthly + epfMonthly;

                for (int i = 0; i < fds.Count; i++)
                {
                    var f = fds[i];
                    if (f.Value == 0) continue;
                    if (f.Left > 0) { f.Value *= 1 + (decimal)(f.Rate / 100 / 12); f.Left--; if (f.Left == 0) { otherDebt += f.Value; f.Value = 0; } }
                    else { otherDebt += f.Value; f.Value = 0; }
                    fds[i] = f;
                }
                for (int i = 0; i < rds.Count; i++)
                {
                    var r = rds[i];
                    if (r.Value == 0 && r.Left == 0) continue;
                    if (r.Left > 0) { r.Value = (r.Value + r.Monthly) * (1 + (decimal)(r.Rate / 100 / 12)); invested += r.Monthly; r.Left--; if (r.Left == 0) { otherDebt += r.Value; r.Value = 0; } }
                    else { otherDebt += r.Value; r.Value = 0; }
                    rds[i] = r;
                }

                target *= infl;
                if (m % 12 == 0 && p.AnnualStepUp > 0)
                {
                    var k = (decimal)(1 + p.AnnualStepUp / 100);
                    eqMonthly *= k; debtMonthly *= k;
                }

                var pv = Portfolio();
                if (hit == null && targetToday > 0 && pv >= target) { hit = m; targetAtHit = target; }
                if (m % 12 == 0) years.Add(new FireYear(m / 12, currentAge + m / 12, pv, target, invested));
                if (hit != null && m >= hit + 60 && m % 12 == 0 && years.Count > 10) break; // show 5 yrs past FIRE
            }
            return new FireResult(targetToday, hit, targetAtHit, years);
        }

        public static FireInputs DefaultInputs(FinanceSnapshot s, int portfolioAgeMonths, bool essentialsOnly = false)
        {
            var m = Fmt.ThisMonth;
            var realized = s.RealizedEquityCagr(portfolioAgeMonths);
            var eqReturn = portfolioAgeMonths >= 36 && realized > 0 && realized < 18 ? realized : s.Settings.ExpectedEquityReturn;
            if (eqReturn <= 0) eqReturn = 12;
            return new FireInputs(
                MonthlyExpense: s.MonthlyCostOfLiving(essentialsOnly),
                EquityMonthly: s.EquitySipMonthly(m),
                DebtMonthly: s.OtherInvestMonthly(m),
                EquityReturn: Math.Round(eqReturn, 1),
                DebtReturn: 7.0,
                Inflation: s.Settings.ExpectedInflation > 0 ? s.Settings.ExpectedInflation : 6,
                Swr: s.Settings.SafeWithdrawalRate > 0 ? s.Settings.SafeWithdrawalRate : 3.5);
        }

        public static int? AgeOf(UserSettings s)
        {
            if (s.DateOfBirth is not DateTime dob || dob.Year < 1900) return null;
            var t = DateTime.Today;
            var age = t.Year - dob.Year - (t.DayOfYear < dob.DayOfYear ? 1 : 0);
            return age is > 0 and < 120 ? age : null;
        }

        // Records one net-worth point per day (fire-and-forget safe)
        public async Task<(decimal Diff, double Pct)?> TrackNetWorthAsync(decimal netWorth)
        {
            var uid = UserId;
            if (string.IsNullOrEmpty(uid) || netWorth <= 0) return null;
            try
            {
                var hist = (await _db.From<NetWorthSnapshot>().Where(x => x.UserId == uid)
                    .Order("recorded_date", Ordering.Descending).Limit(2).Get()).Models;
                var today = DateTime.UtcNow.Date;
                var latest = hist.FirstOrDefault();
                NetWorthSnapshot? prev;
                if (latest != null && latest.RecordedDate.Date == today)
                {
                    if (latest.TotalNetWorth != netWorth)
                        await _db.From<NetWorthSnapshot>().Where(x => x.Id == latest.Id).Set(x => x.TotalNetWorth, netWorth).Update();
                    prev = hist.Skip(1).FirstOrDefault();
                }
                else
                {
                    if (latest == null || latest.TotalNetWorth != netWorth)
                        await _db.From<NetWorthSnapshot>().Insert(new NetWorthSnapshot { UserId = uid, RecordedDate = today, TotalNetWorth = netWorth });
                    prev = latest;
                }
                if (prev == null || prev.TotalNetWorth <= 0) return null;
                var diff = netWorth - prev.TotalNetWorth;
                return (diff, (double)(diff / prev.TotalNetWorth * 100));
            }
            catch { return null; }
        }
    }
}
