# Azure OpenAI Sales Analytics Integration

This document explains how to set up and use Azure OpenAI to analyze sales trends and generate insights for the Retail Monolith application.

## Features

- **AI-Powered Sales Analysis**: Automatically analyzes sales data from your database
- **Trend Detection**: Identifies patterns in daily sales, product performance, and order statuses
- **Actionable Recommendations**: Provides specific, data-driven recommendations
- **Real-time Insights**: Generated on-demand using the latest data

## Prerequisites

1. **Azure OpenAI Service**
   - An active Azure subscription
   - Azure OpenAI resource deployed
   - A deployed GPT-4 or GPT-3.5-turbo model

## Setup Instructions

### 1. Create Azure OpenAI Resource

```bash
# Login to Azure
az login

# Create a resource group (if needed)
az group create --name rg-retail-analytics --location eastus

# Create Azure OpenAI resource
az cognitiveservices account create \
  --name openai-retail-analytics \
  --resource-group rg-retail-analytics \
  --location eastus \
  --kind OpenAI \
  --sku S0

# Deploy a model
az cognitiveservices account deployment create \
  --name openai-retail-analytics \
  --resource-group rg-retail-analytics \
  --deployment-name gpt-4 \
  --model-name gpt-4 \
  --model-version "0613" \
  --model-format OpenAI \
  --sku-capacity 10 \
  --sku-name Standard
```

### 2. Get Your API Credentials

```bash
# Get the endpoint
az cognitiveservices account show \
  --name openai-retail-analytics \
  --resource-group rg-retail-analytics \
  --query properties.endpoint \
  --output tsv

# Get the API key
az cognitiveservices account keys list \
  --name openai-retail-analytics \
  --resource-group rg-retail-analytics \
  --query key1 \
  --output tsv
```

### 3. Configure the Application

**The application now reads credentials from environment variables instead of appsettings.json for security.**

Set the following environment variables:

**Linux/macOS:**
```bash
export AZURE_OPENAI_ENDPOINT="https://your-resource-name.openai.azure.com/"
export AZURE_OPENAI_API_KEY="your-api-key-here"
```

**Windows (Command Prompt):**
```cmd
set AZURE_OPENAI_ENDPOINT=https://your-resource-name.openai.azure.com/
set AZURE_OPENAI_API_KEY=your-api-key-here
```

**Windows (PowerShell):**
```powershell
$env:AZURE_OPENAI_ENDPOINT="https://your-resource-name.openai.azure.com/"
$env:AZURE_OPENAI_API_KEY="your-api-key-here"
```

**For GitHub Codespaces:**
Add `AZURE_OPENAI_ENDPOINT` and `AZURE_OPENAI_API_KEY` as repository secrets under Settings → Secrets and variables → Codespaces.

**For local development with .env file:**
1. Copy `.env.example` to `.env`
2. Update the values in `.env` with your actual credentials
3. The `.env` file is git-ignored to prevent accidental commits

**Alternative: Using .NET User Secrets (for development):**
```bash
dotnet user-secrets init
dotnet user-secrets set "AzureOpenAI:Endpoint" "https://your-resource-name.openai.azure.com/"
dotnet user-secrets set "AzureOpenAI:ApiKey" "your-api-key-here"
dotnet user-secrets set "AzureOpenAI:DeploymentName" "gpt-4"
```

### 4. Install Dependencies

```bash
dotnet restore
```

### 5. Run the Application

```bash
dotnet run
```

Visit `https://localhost:5001/Analytics/Insights` (or use the "📊 Insights" link in the navigation).

## How It Works

### 1. Data Collection
The `AnalyticsService` queries your database to collect:
- Total orders and revenue over the specified period (default: 30 days)
- Average order value
- Orders grouped by status
- Top-selling products
- Daily sales trends

### 2. AI Analysis
The service sends this data to Azure OpenAI with a structured prompt asking for:
- A brief summary of the sales performance
- Key trends and patterns
- Actionable recommendations

### 3. Presentation
The insights are displayed in a user-friendly dashboard with:
- Summary metric cards
- AI-generated insights sections
- Top products table
- Recent daily sales trends

## Using the Insights Page

1. Navigate to **Analytics > Insights** in the menu
2. The page automatically analyzes the last 30 days of sales data
3. Click **Refresh Insights** to regenerate the analysis with current data
4. Review the AI-generated recommendations for business decisions

## Customization

### Adjust Analysis Period

Modify the `DaysBack` parameter in the URL:
```
/Analytics/Insights?days=7   // Last 7 days
/Analytics/Insights?days=90  // Last 90 days
```

### Customize the AI Prompt

Edit `AnalyticsService.cs` method `BuildAnalysisPrompt()` to focus on specific aspects:

```csharp
sb.AppendLine("Focus on customer retention and repeat purchases");
sb.AppendLine("Identify seasonal patterns");
sb.AppendLine("Compare weekday vs weekend performance");
```

### Add Customer Sentiment Analysis

To add review/feedback sentiment analysis, extend the model:

```csharp
public class CustomerReview
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public string CustomerId { get; set; }
    public string ReviewText { get; set; }
    public int Rating { get; set; }
    public DateTime CreatedAt { get; set; }
}
```

Then update the analytics service to include review data in the prompt.

## API Integration

You can also call the analytics service programmatically:

```csharp
// In a controller or API endpoint
public class AnalyticsController : ControllerBase
{
    private readonly IAnalyticsService _analytics;
    
    [HttpGet("api/analytics/sales-data")]
    public async Task<IActionResult> GetSalesData(int days = 30)
    {
        var data = await _analytics.GetSalesDataAsync(days);
        return Ok(data);
    }
    
    [HttpGet("api/analytics/insights")]
    public async Task<IActionResult> GetInsights(int days = 30)
    {
        var insight = await _analytics.GenerateSalesInsightAsync(days);
        return Ok(insight);
    }
}
```

## Cost Considerations

- Each insight generation makes 1 API call to Azure OpenAI
- Approximate token usage: 500-1500 tokens per request (depending on data volume)
- Cost estimate (GPT-4): $0.01 - $0.05 per insight generation
- Consider implementing caching for frequently accessed insights

## Troubleshooting

### "Unable to generate insights"
- Verify your Azure OpenAI endpoint and API key in `appsettings.json`
- Ensure your deployment name matches the one in Azure
- Check that your Azure OpenAI resource has sufficient quota

### "No data available"
- Ensure your database has order data
- Check the date range - try increasing `daysBack`
- Verify database connection string

### Rate Limiting
If you hit rate limits, consider:
- Implementing caching (e.g., cache insights for 1 hour)
- Reducing analysis frequency
- Increasing your Azure OpenAI quota

## Security Best Practices

1. **Never commit API keys** to source control
2. Use **Azure Key Vault** for production secrets
3. Implement **authentication/authorization** for the insights page
4. Enable **Azure Private Link** for network isolation
5. Use **Managed Identity** when deploying to Azure App Service

## Next Steps

- Add customer sentiment analysis from reviews/feedback
- Implement forecast predictions using historical trends
- Create scheduled reports sent via email
- Add export functionality (PDF, Excel)
- Integrate with Power BI for advanced visualizations

## Resources

- [Azure OpenAI Documentation](https://learn.microsoft.com/azure/ai-services/openai/)
- [Azure OpenAI Pricing](https://azure.microsoft.com/pricing/details/cognitive-services/openai-service/)
- [Responsible AI Guidelines](https://www.microsoft.com/ai/responsible-ai)
