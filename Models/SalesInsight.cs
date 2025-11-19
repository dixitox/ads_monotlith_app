namespace RetailMonolith.Models
{
    public class SalesInsight
    {
        public string Summary { get; set; } = string.Empty;
        public string Trends { get; set; } = string.Empty;
        public string Recommendations { get; set; } = string.Empty;
        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    }

    public class SalesAnalysisData
    {
        public int TotalOrders { get; set; }
        public decimal TotalRevenue { get; set; }
        public decimal AverageOrderValue { get; set; }
        public List<ProductSales> TopProducts { get; set; } = new();
        public List<DailySales> DailySales { get; set; } = new();
        public Dictionary<string, int> OrdersByStatus { get; set; } = new();
    }

    public class ProductSales
    {
        public string Name { get; set; } = string.Empty;
        public int QuantitySold { get; set; }
        public decimal Revenue { get; set; }
    }

    public class DailySales
    {
        public DateTime Date { get; set; }
        public int OrderCount { get; set; }
        public decimal Revenue { get; set; }
    }
}
