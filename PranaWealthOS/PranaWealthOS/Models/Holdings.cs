using Postgrest.Attributes;
using Postgrest.Models;
using System;

namespace PranaWealthOS.Models
{
    // 1. Tell Supabase which table this class belongs to
    [Table("user_settings")]
    public class UserSettings : BaseModel // 2. Must inherit from BaseModel
    {
        // 3. Map every property to its exact SQL column name
        [PrimaryKey("id", false)] // 'false' means we will provide the ID manually (the Auth User ID)
        public string Id { get; set; }

        [Column("user_name")]
        public string UserName { get; set; }

        [Column("date_of_birth")]
        public DateTime? DateOfBirth { get; set; } = DateTime.Today.AddYears(-30);

        [Column("monthly_income")]
        public decimal MonthlyIncome { get; set; }

        [Column("monthly_sip")]
        public decimal MonthlySIP { get; set; }

        [Column("expected_inflation")]
        public double ExpectedInflation { get; set; } = 6.0;

        [Column("expected_equity_return")]
        public double ExpectedEquityReturn { get; set; } = 12.0;

        [Column("safe_withdrawal_rate")]
        public double SafeWithdrawalRate { get; set; } = 3.5;
    }

    // -----------------------------------------------------
    // THE MASTER ASSET MODEL (Matches the Supabase Table)
    // -----------------------------------------------------
    [Table("assets")]
    public class AssetModel : BaseModel
    {
        [PrimaryKey("id", false)]
        public string Id { get; set; }

        [Column("user_id")]
        public string UserId { get; set; }

        [Column("asset_type")]
        public string AssetType { get; set; }

        [Column("name")]
        public string Name { get; set; }

        [Column("invested_value")]
        public decimal InvestedValue { get; set; }

        [Column("current_value")]
        public decimal CurrentValue { get; set; }

        [Column("category")]
        public string Category { get; set; } = "Uncategorized";

        [Column("is_short_term_goal")]
        public bool IsShortTermGoal { get; set; }

        [Column("monthly_contribution")]
        public decimal MonthlyContribution { get; set; }

        [Column("interest_rate")]
        public double InterestRate { get; set; }

        [Column("months_duration")]
        public int MonthsDuration { get; set; }

        [Column("is_bond")]
        public bool IsBond { get; set; }
    }

    public class StockHolding
    {
        public string Id { get; set; }
        public string StockName { get; set; }
        public decimal InvestedValue { get; set; }
        public decimal ClosingValue { get; set; }
        public string Category { get; set; } = "Uncategorized";
        public bool IsShortTermGoal { get; set; }
    }

    public class MutualFundHolding
    {
        public string Id { get; set; }
        public string SchemeName { get; set; }
        public decimal InvestedValue { get; set; }
        public decimal CurrentValue { get; set; }
        public string Category { get; set; } = "Uncategorized";
        public bool IsShortTermGoal { get; set; }
    }

    public class FixedDeposit
    {
        public string Id { get; set; }
        public decimal Principal { get; set; }
        public double InterestRate { get; set; }
        public int MonthsDuration { get; set; }
        public bool IsBond { get; set; }
    }

    public class RecurringDeposit
    {
        public string Id { get; set; }
        public decimal CurrentValue { get; set; }
        public decimal MonthlyInvestment { get; set; }
        public double InterestRate { get; set; }
        public int MonthsLeft { get; set; }
    }

    public class EPFData
    {
        public string Id { get; set; }
        public decimal CurrentValue { get; set; }
        public decimal MonthlyContribution { get; set; }
        public double InterestRate { get; set; } = 8.25;
    }

    public class PhysicalAsset
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public decimal Value { get; set; }
    }

    [Table("expenses")]
    public class ExpenseItem : BaseModel
    {
        // 'false' tells C# to let Supabase auto-generate the UUID when we add a new expense
        [PrimaryKey("id", false)]
        public string Id { get; set; }

        [Column("user_id")]
        public string UserId { get; set; }

        [Column("name")]
        public string Name { get; set; }

        [Column("amount")]
        public decimal Amount { get; set; }

        [Column("category")]
        public string Category { get; set; }

        [Column("is_mandatory")]
        public bool IsMandatory { get; set; } = true;
    }

    [Table("net_worth_history")]
    public class NetWorthSnapshot : BaseModel
    {
        [PrimaryKey("id", false)]
        public string Id { get; set; }

        [Column("user_id")]
        public string UserId { get; set; }

        [Column("recorded_date")]
        public DateTime RecordedDate { get; set; }

        [Column("total_net_worth")]
        public decimal TotalNetWorth { get; set; }
    }

    // Used in the FIRE Strategy Page
    public class ProjectionYear
    {
        public int YearNumber { get; set; }
        public decimal PortfolioValue { get; set; }
        public decimal TargetValue { get; set; }
    }

    public class NewsItem { public string Title { get; set; } public string Link { get; set; } }
}