# Quick Start: Azure OpenAI Sales Analytics

## What Was Added

✅ **Azure OpenAI Integration** - AI-powered sales insights
✅ **Analytics Service** - Collects and analyzes sales data
✅ **Insights Dashboard** - Beautiful UI to view AI-generated recommendations
✅ **Navigation Link** - Easy access from the main menu (📊 Insights)

## Quick Setup (5 minutes)

### 1. Create Azure OpenAI Resource

**Option A: Azure Portal**
1. Go to [Azure Portal](https://portal.azure.com)
2. Create a new "Azure OpenAI" resource
3. Deploy a model (GPT-4 or GPT-3.5-turbo)
4. Copy the endpoint and API key

**Option B: Azure CLI**
```bash
az cognitiveservices account create \
  --name openai-retail \
  --resource-group your-rg \
  --location eastus \
  --kind OpenAI \
  --sku S0
```

### 2. Configure Your App

Edit `appsettings.json`:

```json
{
  "AzureOpenAI": {
    "Endpoint": "https://YOUR-RESOURCE-NAME.openai.azure.com/",
    "ApiKey": "YOUR-API-KEY",
    "DeploymentName": "gpt-4"
  }
}
```

### 3. Run the App

```bash
dotnet run
```

### 4. View Insights

Navigate to: **https://localhost:5001/Analytics/Insights**

Or click the **📊 Insights** link in the navigation menu.

## What You'll See

- **Summary Cards**: Total orders, revenue, average order value
- **AI Summary**: High-level overview of your sales performance
- **Trends**: Key patterns identified by AI
- **Recommendations**: Actionable suggestions to improve sales
- **Top Products**: Best-selling items with revenue
- **Daily Sales**: Recent performance trends

## Features

- 📊 Real-time sales analysis
- 🤖 AI-powered insights using GPT-4
- 📈 Trend detection and pattern analysis
- 💡 Actionable business recommendations
- 🔄 On-demand refresh
- 📅 Customizable time periods (7, 30, 90 days)

## Files Added/Modified

### New Files
- `Models/SalesInsight.cs` - Data models for insights
- `Services/IAnalyticsService.cs` - Service interface
- `Services/AnalyticsService.cs` - Core analytics logic with Azure OpenAI
- `Pages/Analytics/Insights.cshtml` - Dashboard UI
- `Pages/Analytics/Insights.cshtml.cs` - Page model
- `AZURE_OPENAI_SETUP.md` - Detailed documentation

### Modified Files
- `RetailMonolith.csproj` - Added Azure.AI.OpenAI package
- `Program.cs` - Registered AnalyticsService
- `appsettings.json` - Added Azure OpenAI configuration
- `Pages/Shared/_Layout.cshtml` - Added navigation link

## Cost Estimate

- ~$0.01 - $0.05 per insight generation with GPT-4
- ~$0.001 - $0.005 per insight with GPT-3.5-turbo

## Security Notes

⚠️ **For Production:**
- Use Azure Key Vault for secrets
- Enable Managed Identity
- Never commit API keys to source control
- Use User Secrets for development:

```bash
dotnet user-secrets set "AzureOpenAI:ApiKey" "your-key"
```

## Next Steps

1. ✅ Set up Azure OpenAI (see above)
2. 🔄 Generate your first insights
3. 📧 Add scheduled email reports
4. 💬 Add customer sentiment analysis from reviews
5. 📊 Export insights to PDF/Excel

## Need Help?

See `AZURE_OPENAI_SETUP.md` for:
- Detailed setup instructions
- Customization options
- API integration examples
- Troubleshooting guide
- Security best practices

## Example Insights Generated

**Summary:**
"Sales performance over the last 30 days shows strong growth with 156 orders generating $12,450 in revenue. The average order value of $79.81 indicates healthy purchasing behavior."

**Trends:**
- Weekend sales are 40% higher than weekdays
- Electronics category dominates with 60% of revenue
- Conversion rate increased 15% week-over-week

**Recommendations:**
1. Increase inventory for top-selling electronics
2. Launch weekend-specific promotions
3. Focus marketing on high-converting categories
4. Consider bundle deals to increase order value
5. Analyze cart abandonment on weekdays

---

**Ready to get started?** Just configure your Azure OpenAI credentials and refresh the Insights page!
