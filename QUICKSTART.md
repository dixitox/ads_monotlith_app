## Azure OpenAI Retail Chat Assistant - Quick Start

### ✅ What's Been Implemented

1. **Azure OpenAI SDK Integration**
   - Added Azure.AI.OpenAI package v2.1.0
   - Configured for GPT-4/GPT-5 models

2. **Backend Services**
   - `IChatService` interface
   - `ChatService` implementation with:
     - Dynamic system prompt generation from product database
     - Conversation history management
     - Azure OpenAI API integration
     - Error handling and logging

3. **API Endpoint**
   - `/api/chat` POST endpoint
   - Accepts message and conversation history
   - Returns AI-generated responses

4. **Frontend Chat UI**
   - Beautiful animated chat modal
   - Fixed bottom-left button (always visible)
   - Conversation history display
   - Typing indicators
   - Responsive design

5. **Configuration**
   - `appsettings.json` with Azure OpenAI settings
   - Dependency injection setup in `Program.cs`

### 🚀 To Get Started

1. **Configure Azure OpenAI** (in `appsettings.json`):
   ```json
   "AzureOpenAI": {
     "Endpoint": "https://YOUR_RESOURCE_NAME.openai.azure.com/",
     "ApiKey": "YOUR_API_KEY",
     "DeploymentName": "gpt-4",
     "MaxTokens": 800,
     "Temperature": 0.7
   }
   ```

2. **Run the application**:
   ```bash
   dotnet run
   ```

3. **Test the chat**:
   - Click the purple chat button in the bottom-left corner
   - Try: "What products do you have?"
   - Try: "Show me items under £50"
   - Try: "I need gift recommendations"

### 🎯 How It Works

The AI assistant has context about:
- All products in your catalog
- Product categories
- Pricing information
- Product descriptions
- SKU codes

It can help customers:
- Find products by category
- Get recommendations based on price
- Navigate to cart, checkout, and orders
- Answer questions about the store

### 📝 Test Queries to Try

1. "What categories of products do you sell?"
2. "Show me some Apparel items"
3. "What's the cheapest Electronics product?"
4. "I'm looking for something under £20"
5. "How do I view my cart?"
6. "Where can I see my order history?"
7. "Can you recommend a gift?"
8. "What's your most expensive product?"

### 🔧 Customization

**Change AI personality**: Edit `BuildSystemPromptAsync()` in `Services/ChatService.cs`

**Modify UI colors**: Update gradient colors in `_Layout.cshtml` styles:
```css
background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
```

**Adjust response length**: Change `MaxTokens` in configuration

**Add product images**: Extend the system prompt to include image URLs

### ⚠️ Important Notes

- You MUST configure Azure OpenAI credentials before the chat will work
- The chat button appears on every page (it's in _Layout.cshtml)
- Conversation history is maintained during the session
- Costs apply based on Azure OpenAI token usage

### 📚 Full Documentation

See `CHAT_ASSISTANT_SETUP.md` for:
- Complete setup instructions
- Security best practices
- Cost optimization tips
- Advanced customization options
- Troubleshooting guide
