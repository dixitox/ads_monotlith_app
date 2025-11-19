using RetailMonolith.Models;

namespace RetailMonolith.Services
{
    public interface IAnalyticsService
    {
        Task<SalesAnalysisData> GetSalesDataAsync(int daysBack = 30);
        Task<SalesInsight> GenerateSalesInsightAsync(int daysBack = 30);
    }
}
