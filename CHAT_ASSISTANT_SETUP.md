# Azure OpenAI Retail Chat Assistant - Setup Guide

## Overview
This retail chat assistant is powered by Azure OpenAI (GPT-4/GPT-5) and provides intelligent product recommendations, shopping assistance, and customer support for your RetailMonolith application.

## Features
✅ **AI-Powered Assistance**: Uses Azure OpenAI for natural language understanding
✅ **Product Knowledge**: Has access to your entire product catalog with real-time data
✅ **Contextual Responses**: Understands categories, pricing, inventory, and can make personalized recommendations
✅ **Conversation History**: Maintains context throughout the chat session
✅ **Beautiful UI**: Modern, responsive chat interface with smooth animations
✅ **Fixed Position Button**: Always accessible from any page on your site

## Configuration Steps

### 1. Set Up Azure OpenAI Service

1. **Create Azure OpenAI Resource**:
   - Go to [Azure Portal](https://portal.azure.com)
   - Create a new "Azure OpenAI" resource
   - Note your endpoint URL (e.g., `https://YOUR_RESOURCE_NAME.openai.azure.com/`)

2. **Deploy a Model**:
   - In your Azure OpenAI resource, go to "Model deployments"
   - Deploy GPT-4 or GPT-4 Turbo (or the latest GPT-5 if available)
   - Note your deployment name (e.g., `gpt-4`, `gpt-4-turbo`, `gpt-5`)

3. **Get API Key**:
   - Go to "Keys and Endpoint" in your Azure OpenAI resource
   - Copy one of the API keys

### 2. Update Configuration

**The application now reads credentials from environment variables instead of appsettings.json for security.**

Set the following environment variables:

**Linux/macOS:**
```bash
export AZURE_OPENAI_ENDPOINT="https://YOUR_RESOURCE_NAME.openai.azure.com/"
export AZURE_OPENAI_API_KEY="your-api-key-here"
```

**Windows (Command Prompt):**
```cmd
set AZURE_OPENAI_ENDPOINT=https://YOUR_RESOURCE_NAME.openai.azure.com/
set AZURE_OPENAI_API_KEY=your-api-key-here
```

**Windows (PowerShell):**
```powershell
$env:AZURE_OPENAI_ENDPOINT="https://YOUR_RESOURCE_NAME.openai.azure.com/"
$env:AZURE_OPENAI_API_KEY="your-api-key-here"
```

**For GitHub Codespaces:**
Add `AZURE_OPENAI_ENDPOINT` and `AZURE_OPENAI_API_KEY` as repository secrets under Settings → Secrets and variables → Codespaces.

**For local development with .env file:**
1. Copy `.env.example` to `.env`
2. Update the values in `.env` with your actual credentials
3. The `.env` file is git-ignored to prevent accidental commits

**Note:** The deployment name, max tokens, and temperature are still configured in `appsettings.json`:
```json
"AzureOpenAI": {
  "DeploymentName": "gpt-4",
  "MaxTokens": 800,
  "Temperature": 0.7
}
```

### 3. For Production: Use Azure Key Vault

For production deployments on Azure, consider using Azure Key Vault or Managed Identity for enhanced security. The environment variable approach works well for both local development and cloud deployments when secrets are properly managed through GitHub Secrets, Azure App Service configuration, or similar secure storage.

## How It Works

### Architecture

1. **Frontend (JavaScript)**: 
   - Chat UI in `_Layout.cshtml`
   - Handles user input and displays responses
   - Manages conversation history

2. **API Endpoint** (`/api/chat`):
   - Receives chat messages
   - Passes to ChatService
   - Returns AI responses

3. **ChatService**:
   - Builds system prompt with real-time product data
   - Calls Azure OpenAI API
   - Maintains context with conversation history

4. **System Prompt**:
   - Dynamically generated from database
   - Includes product catalog, categories, prices
   - Provides instructions for the AI assistant

### Data Flow

```
User Input → JavaScript → /api/chat Endpoint → ChatService → Azure OpenAI
                                                      ↓
                                            Loads Product Data from DB
                                                      ↓
                                            Builds Context & Prompt
                                                      ↓
Azure OpenAI Response ← ChatService ← API Response ← JavaScript ← Display
```

## Customization

### Modify AI Behavior

Edit `Services/ChatService.cs` in the `BuildSystemPromptAsync` method to:
- Add more product details
- Include inventory information
- Add order history context
- Change tone and style
- Add business rules

### UI Customization

Edit styles in `Pages/Shared/_Layout.cshtml`:
- Colors: Modify gradient colors in `.fixed-bottom-left-btn` and `.chat-header`
- Size: Change width/height in `.chat-modal`
- Position: Adjust `bottom` and `left` values
- Animation: Modify `@keyframes` sections

### Add More Features

1. **Product Search Integration**:
   ```csharp
   // In ChatService.cs, add product search capability
   var searchResults = await _db.Products
       .Where(p => p.Name.Contains(searchTerm) || p.Description.Contains(searchTerm))
       .ToListAsync();
   ```

2. **Order Status Lookup**:
   ```csharp
   // Add method to check order status
   var orders = await _db.Orders
       .Where(o => o.CustomerId == customerId)
       .Include(o => o.Lines)
       .OrderByDescending(o => o.CreatedUtc)
       .ToListAsync();
   ```

3. **Cart Integration**:
   - Inject `ICartService` into `ChatService`
   - Include cart contents in system prompt
   - Allow AI to suggest adding items to cart

## Testing

1. **Build the project**:
   ```bash
   dotnet build
   ```

2. **Run the application**:
   ```bash
   dotnet run
   ```

3. **Test the chat**:
   - Open the website in a browser
   - Click the purple chat button in the bottom-left
   - Try these test queries:
     - "What products do you have in the Apparel category?"
     - "Show me products under £30"
     - "What's the most expensive item you have?"
     - "I need a gift recommendation"
     - "How do I check my orders?"

## Troubleshooting

### Error: "Azure OpenAI Endpoint not configured"
- Make sure you've updated `appsettings.json` with your actual endpoint URL

### Error: "401 Unauthorized"
- Check that your API key is correct
- Verify the key hasn't expired
- Ensure you're using the correct endpoint

### Error: "404 Model Not Found"
- Verify your deployment name matches what's in Azure
- Check that the model is actually deployed in Azure Portal

### Empty or Generic Responses
- Check that products exist in the database
- Verify `SeedAsync` method ran successfully
- Check logs for any database connection issues

### Chat Not Opening
- Open browser console (F12) and check for JavaScript errors
- Verify all files saved correctly
- Clear browser cache

## Cost Considerations

Azure OpenAI charges based on:
- **Tokens used**: Input + Output tokens
- **Model type**: GPT-4 costs more than GPT-3.5

To optimize costs:
1. Reduce `MaxTokens` in configuration (fewer words in responses)
2. Limit conversation history length (currently set to last 10 exchanges)
3. Use GPT-3.5-turbo instead of GPT-4 for testing
4. Implement caching for common queries
5. Set token limits per user/session

## Security Best Practices

1. **Never commit API keys** to source control
2. Use Azure Key Vault in production
3. Implement rate limiting on the `/api/chat` endpoint
4. Add authentication to prevent abuse
5. Sanitize user input before sending to AI
6. Monitor usage and set budget alerts in Azure

## Next Steps

- [ ] Add user authentication
- [ ] Implement conversation persistence
- [ ] Add product image support in chat
- [ ] Enable voice input/output
- [ ] Add analytics to track chat usage
- [ ] Implement A/B testing for different prompts
- [ ] Add multi-language support
- [ ] Create admin dashboard for chat analytics

## Support

For issues or questions:
- Check Azure OpenAI documentation: https://learn.microsoft.com/azure/ai-services/openai/
- Review logs in `/logs` directory
- Check Application Insights if configured

---

**Note**: This implementation is production-ready but should be enhanced with proper authentication, rate limiting, and monitoring before deploying to a live environment.
