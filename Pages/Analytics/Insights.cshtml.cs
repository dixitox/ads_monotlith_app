using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RetailMonolith.Models;
using RetailMonolith.Services;

namespace RetailMonolith.Pages.Analytics
{
    public class InsightsModel : PageModel
    {
        private readonly IAnalyticsService _analyticsService;
        private readonly ILogger<InsightsModel> _logger;

        public InsightsModel(IAnalyticsService analyticsService, ILogger<InsightsModel> logger)
        {
            _analyticsService = analyticsService;
            _logger = logger;
        }

        public SalesAnalysisData? SalesData { get; set; }
        public SalesInsight? Insight { get; set; }
        public bool IsLoading { get; set; }
        public string? ErrorMessage { get; set; }
        public int DaysBack { get; set; } = 30;

        public async Task OnGetAsync(int days = 30)
        {
            DaysBack = days;
            await LoadInsightsAsync();
        }

        public async Task<IActionResult> OnPostAsync(int days = 30)
        {
            DaysBack = days;
            await LoadInsightsAsync();
            return Page();
        }

        private async Task LoadInsightsAsync()
        {
            try
            {
                IsLoading = false;
                SalesData = await _analyticsService.GetSalesDataAsync(DaysBack);
                Insight = await _analyticsService.GenerateSalesInsightAsync(DaysBack);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading sales insights");
                ErrorMessage = $"Unable to load insights: {ex.Message}";
            }
        }
    }
}
