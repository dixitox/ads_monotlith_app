using Azure;
using Azure.AI.OpenAI;
using Microsoft.EntityFrameworkCore;
using OpenAI.Chat;
using RetailMonolith.Data;
using RetailMonolith.Models;
using System.Text;
using System.Text.Json;

namespace RetailMonolith.Services
{
    public class AnalyticsService : IAnalyticsService
    {
        private readonly AppDbContext _db;
        private readonly AzureOpenAIClient _aiClient;
        private readonly string _deploymentName;
        private readonly ILogger<AnalyticsService> _logger;

        public AnalyticsService(
            AppDbContext db,
            IConfiguration configuration,
            ILogger<AnalyticsService> logger)
        {
            _db = db;
            _logger = logger;

            var endpoint = configuration["AzureOpenAI:Endpoint"] 
                ?? throw new InvalidOperationException("AzureOpenAI:Endpoint not configured");
            var apiKey = configuration["AzureOpenAI:ApiKey"] 
                ?? throw new InvalidOperationException("AzureOpenAI:ApiKey not configured");
            _deploymentName = configuration["AzureOpenAI:DeploymentName"] 
                ?? throw new InvalidOperationException("AzureOpenAI:DeploymentName not configured");

            _aiClient = new AzureOpenAIClient(new Uri(endpoint), new AzureKeyCredential(apiKey));
        }

        public async Task<SalesAnalysisData> GetSalesDataAsync(int daysBack = 30)
        {
            var startDate = DateTime.UtcNow.AddDays(-daysBack);

            var orders = await _db.Orders
                .Include(o => o.Lines)
                .Where(o => o.CreatedUtc >= startDate)
                .ToListAsync();

            var analysisData = new SalesAnalysisData
            {
                TotalOrders = orders.Count,
                TotalRevenue = orders.Sum(o => o.Total),
                AverageOrderValue = orders.Any() ? orders.Average(o => o.Total) : 0,
                OrdersByStatus = orders.GroupBy(o => o.Status)
                    .ToDictionary(g => g.Key, g => g.Count()),
                DailySales = orders.GroupBy(o => o.CreatedUtc.Date)
                    .Select(g => new DailySales
                    {
                        Date = g.Key,
                        OrderCount = g.Count(),
                        Revenue = g.Sum(o => o.Total)
                    })
                    .OrderBy(d => d.Date)
                    .ToList(),
                TopProducts = orders.SelectMany(o => o.Lines)
                    .GroupBy(l => l.Name)
                    .Select(g => new ProductSales
                    {
                        Name = g.Key,
                        QuantitySold = g.Sum(l => l.Quantity),
                        Revenue = g.Sum(l => l.UnitPrice * l.Quantity)
                    })
                    .OrderByDescending(p => p.Revenue)
                    .Take(10)
                    .ToList()
            };

            return analysisData;
        }

        public async Task<SalesInsight> GenerateSalesInsightAsync(int daysBack = 30)
        {
            try
            {
                // Get sales data
                var salesData = await GetSalesDataAsync(daysBack);

                // Build prompt for Azure OpenAI
                var prompt = BuildAnalysisPrompt(salesData, daysBack);

                // Call Azure OpenAI
                var chatClient = _aiClient.GetChatClient(_deploymentName);
                
                var messages = new List<ChatMessage>
                {
                    new SystemChatMessage("You are a retail analytics expert. Analyze sales data and provide actionable insights in a clear, concise manner. Focus on trends, patterns, and specific recommendations."),
                    new UserChatMessage(prompt)
                };

                var response = await chatClient.CompleteChatAsync(messages);
                var insightText = response.Value.Content[0].Text;

                // Parse the response into structured insight
                var insight = ParseInsightResponse(insightText);
                insight.GeneratedAt = DateTime.UtcNow;

                return insight;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating sales insight");
                return new SalesInsight
                {
                    Summary = "Unable to generate insights at this time.",
                    Trends = "Error occurred while analyzing data.",
                    Recommendations = "Please check your Azure OpenAI configuration.",
                    GeneratedAt = DateTime.UtcNow
                };
            }
        }

        private string BuildAnalysisPrompt(SalesAnalysisData data, int daysBack)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Analyze the following sales data from the last {daysBack} days and provide insights:");
            sb.AppendLine();
            sb.AppendLine($"**Overall Metrics:**");
            sb.AppendLine($"- Total Orders: {data.TotalOrders}");
            sb.AppendLine($"- Total Revenue: ${data.TotalRevenue:N2}");
            sb.AppendLine($"- Average Order Value: ${data.AverageOrderValue:N2}");
            sb.AppendLine();

            if (data.OrdersByStatus.Any())
            {
                sb.AppendLine($"**Orders by Status:**");
                foreach (var status in data.OrdersByStatus)
                {
                    sb.AppendLine($"- {status.Key}: {status.Value} orders");
                }
                sb.AppendLine();
            }

            if (data.TopProducts.Any())
            {
                sb.AppendLine($"**Top 5 Products:**");
                foreach (var product in data.TopProducts.Take(5))
                {
                    sb.AppendLine($"- {product.Name}: {product.QuantitySold} units, ${product.Revenue:N2} revenue");
                }
                sb.AppendLine();
            }

            if (data.DailySales.Any())
            {
                sb.AppendLine($"**Daily Sales Trend (last 7 days):**");
                foreach (var day in data.DailySales.TakeLast(7))
                {
                    sb.AppendLine($"- {day.Date:MMM dd}: {day.OrderCount} orders, ${day.Revenue:N2}");
                }
                sb.AppendLine();
            }

            sb.AppendLine("Please provide:");
            sb.AppendLine("1. SUMMARY: A brief overview (2-3 sentences)");
            sb.AppendLine("2. TRENDS: Key trends and patterns you observe");
            sb.AppendLine("3. RECOMMENDATIONS: 3-5 specific, actionable recommendations");
            sb.AppendLine();
            sb.AppendLine("Format your response with clear section headers.");

            return sb.ToString();
        }

        private SalesInsight ParseInsightResponse(string response)
        {
            var insight = new SalesInsight();
            var sections = response.Split(new[] { "SUMMARY:", "TRENDS:", "RECOMMENDATIONS:" }, 
                StringSplitOptions.RemoveEmptyEntries);

            if (sections.Length >= 3)
            {
                insight.Summary = sections[1].Trim();
                insight.Trends = sections[2].Trim();
                insight.Recommendations = sections.Length > 3 ? sections[3].Trim() : sections[2].Trim();
            }
            else
            {
                // Fallback if parsing fails
                insight.Summary = response.Length > 500 ? response.Substring(0, 500) : response;
                insight.Trends = "See summary for details";
                insight.Recommendations = "See summary for recommendations";
            }

            return insight;
        }
    }
}
