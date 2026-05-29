namespace PranaWealthOS.Models
{
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
        public bool IsShortTermGoal => SchemeName.Contains("Tata Ultra Short Term");
    }
}