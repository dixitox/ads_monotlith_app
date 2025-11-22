# AI Copilot - Complete Setup Guide

**AI-Powered Shopping Assistant for RetailDecomposed**

This guide provides a complete, step-by-step walkthrough for setting up and using the AI Copilot feature with Azure AI Foundry and Entra ID authentication.

---

## 📋 Table of Contents

1. [Overview](#overview)
2. [Prerequisites](#prerequisites)
3. [Step-by-Step Setup](#step-by-step-setup)
4. [Configuration Reference](#configuration-reference)
5. [Testing & Verification](#testing--verification)
6. [Features & Usage](#features--usage)
7. [Architecture](#architecture)
8. [Customization](#customization)
9. [Troubleshooting](#troubleshooting)
10. [Production Deployment](#production-deployment)
11. [API Reference](#api-reference)

---

## 🎯 Overview

The AI Copilot is an intelligent shopping assistant powered by Azure AI Foundry that helps customers:

- Find products based on their needs and preferences
- Get detailed product information (prices, descriptions, categories)
- Receive personalized product recommendations
- Ask questions about the product catalog in natural language

**Authentication:** This application uses **Entra ID authentication only** (no API keys). This provides secure, keyless authentication using:
- **Managed Identity** (when deployed to Azure)
- **Azure CLI** credentials (local development)
- **Visual Studio/VS Code** credentials (local development)

---

## 📋 Prerequisites

Before you begin, ensure you have:

- ✅ **Azure subscription** with appropriate permissions
- ✅ **.NET 9.0 SDK** installed
- ✅ **Azure CLI** installed ([Download here](https://learn.microsoft.com/cli/azure/install-azure-cli))
- ✅ **Access to Azure AI Foundry** (ai.azure.com)
- ✅ **Permissions** to create Azure AI resources and assign roles

---

## 🚀 Step-by-Step Setup

### Step 1: Create Azure AI Foundry Project

1. Navigate to [Azure AI Foundry](https://ai.azure.com)
2. Sign in with your Azure account
3. Click **+ New project** (or select an existing project)
4. Fill in the project details:
   - **Name**: Choose a meaningful name (e.g., `retail-copilot`)
   - **Subscription**: Select your Azure subscription
   - **Resource Group**: Select or create a resource group
5. Click **Create**

### Step 2: Deploy GPT Model

1. In your Azure AI Foundry project, navigate to **Deployments** in the left menu
2. Click **+ Create new deployment**
3. Select a model:
   - **Recommended**: GPT-4o (best performance)
   - **Alternative**: GPT-4, GPT-3.5-turbo
4. Configure deployment:
   - **Deployment name**: `gpt-4o` (remember this name)
   - **Model version**: Latest available
5. Click **Create**
6. Wait for deployment to complete (usually 1-2 minutes)

### Step 3: Get Your Endpoint URL

1. In your Azure AI Foundry project, navigate to your deployment
2. Click on your model deployment (e.g., `gpt-4o`)
3. Look for the **Target URI** or **Endpoint**
4. Copy the endpoint URL - it should be in one of these formats:
   - **Format 1**: `https://<resource-name>.openai.azure.com/` (Azure OpenAI resource)
   - **Format 2**: `https://<project-name>.services.ai.azure.com/openai` (Azure AI Foundry)
   
   ⚠️ **Important**: The endpoint should end with `.openai.azure.com/` or `.ai.azure.com/openai`
   
   ❌ **NOT**: `...ai.azure.com/api/projects/...` (this is the project URL, not the endpoint)

5. Also note your **Resource Name** from Azure Portal for role assignments

### Step 4: Set Up Authentication (Local Development)

Open a terminal and run:

```bash
# Sign in to Azure CLI
az login
```

Assign yourself the "Cognitive Services OpenAI User" role:

```bash
az role assignment create \
  --role "Cognitive Services OpenAI User" \
  --assignee your-email@domain.com \
  --scope /subscriptions/<subscription-id>/resourceGroups/<resource-group>/providers/Microsoft.CognitiveServices/accounts/<ai-resource-name>
```

**How to find the values:**

- **your-email@domain.com**: Your Azure account email
- **subscription-id**: Run `az account show --query id -o tsv`
- **resource-group**: The resource group containing your AI resource
- **ai-resource-name**: Your Azure AI resource name (from Azure Portal)

### Step 5: Configure the Application

1. Navigate to your project folder:
   ```bash
   cd RetailDecomposed
   ```

2. Open `appsettings.Development.json` in your editor

3. Update the `AzureAI` section:
   ```json
   {
     "AzureAI": {
       "Endpoint": "https://your-project.openai.azure.com/",
       "DeploymentName": "gpt-4o",
       "MaxTokens": 800,
       "Temperature": 0.7
     }
   }
   ```

4. Replace `your-project` with your actual project endpoint from Step 3

5. Ensure `DeploymentName` matches what you named your deployment in Step 2

### Step 6: Verify Your Setup

Before running the app, verify your authentication:

```bash
# Verify you're logged in to the correct account
az account show

# Test access to your AI resource
az cognitiveservices account show \
  --name <ai-resource-name> \
  --resource-group <resource-group>
```

If the commands succeed, you're ready to go!

### Step 7: Run the Application

```bash
# Make sure you're in the RetailDecomposed folder
cd RetailDecomposed

# Restore dependencies (if needed)
dotnet restore

# Run the application
dotnet run
```

You should see output like:
```
Initializing Azure AI client with Entra ID authentication
Now listening on: https://localhost:6068
```

### Step 8: Test the AI Assistant

1. Open your browser and navigate to `https://localhost:6068`
2. Click **AI Assistant** in the navigation bar
3. Try a test message:
   - "What products do you have?"
   - "Show me laptops"
   - "I need a gift under $100"
4. You should receive a response from the AI assistant!

---

## ⚙️ Configuration Reference

### Configuration Parameters

| Parameter | Description | Default | Required |
|-----------|-------------|---------|----------|
| `Endpoint` | Your Azure AI Foundry project endpoint URL | - | ✅ Yes |
| `DeploymentName` | Name of your deployed GPT model | `gpt-4o` | ✅ Yes |
| `MaxTokens` | Maximum length of AI response (in tokens) | `800` | No |
| `Temperature` | Response creativity (0.0 = focused, 1.0 = creative) | `0.7` | No |

### Example Configuration

**Development** (`appsettings.Development.json`):
```json
{
  "AzureAI": {
    "Endpoint": "https://my-retail-project.openai.azure.com/",
    "DeploymentName": "gpt-4o",
    "MaxTokens": 800,
    "Temperature": 0.7
  }
}
```

**Production** (`appsettings.json`):
```json
{
  "AzureAI": {
    "Endpoint": "https://my-retail-project-prod.openai.azure.com/",
    "DeploymentName": "gpt-4o",
    "MaxTokens": 1000,
    "Temperature": 0.6
  }
}
```

### Authentication Flow

The application uses `DefaultAzureCredential` which tries these methods **in order**:

1. **Environment Variables** - Service principal credentials
2. **Managed Identity** - When deployed to Azure (App Service, Container Apps, VMs)
3. **Visual Studio** - Uses your VS credentials
4. **Visual Studio Code** - Uses your VS Code Azure credentials
5. **Azure CLI** - Uses credentials from `az login`
6. **Azure PowerShell** - Uses Azure PowerShell credentials
7. **Interactive Browser** - Prompts for login (fallback)

---

## 🧪 Testing & Verification

### Check Application Logs

When the app starts, look for:
```
info: RetailDecomposed.Services.CopilotService[0]
      Initializing Azure AI client with Entra ID authentication
```

### Test Chat Functionality

Try these example questions:

**Product Discovery:**
- "What laptops do you have?"
- "Show me all headphones"
- "List gaming products"

**Recommendations:**
- "I need a gift for a tech enthusiast"
- "What's the best phone under $500?"
- "Recommend something for gaming"

**Product Comparison:**
- "Compare iPhone and Samsung Galaxy"
- "What's the difference between these laptops?"

**Price Queries:**
- "Show me products under $100"
- "What are your most expensive items?"

### Verify Response Quality

A good response should:
- ✅ Reference actual products from your catalog
- ✅ Include prices in the correct format
- ✅ Be conversational and helpful
- ✅ Answer the specific question asked

---

## 🎨 Features & Usage

### Intelligent Product Recommendations

The AI assistant has access to your entire product catalog and can:
- Recommend products based on customer needs
- Answer questions about specific products
- Compare products and features
- Suggest alternatives based on budget or preferences

### Conversation Context

- Maintains conversation history within a session
- Remembers previous questions for contextual responses
- Provides coherent multi-turn conversations
- Automatically limits history to last 10 messages for performance

### Natural Language Understanding

- Understands casual questions and requests
- Handles various phrasings and intents
- Provides friendly, conversational responses
- Adapts tone based on the query

### Example Conversations

**Example 1: Product Discovery**
```
User: "What laptops do you have?"
AI: "We have several great laptop options available:

1. Dell XPS 13 - $1,299.99
   - Perfect for professionals, ultra-portable with excellent battery life
   
2. MacBook Air M2 - $1,199.00
   - Great for creative work, excellent performance
   
Which features are most important to you?"
```

**Example 2: Recommendations**
```
User: "I need a gift for someone who loves gaming, budget around $200"
AI: "For a gaming enthusiast with a $200 budget, I'd recommend:

1. Gaming Headset Pro X - $149.99
   - Immersive 7.1 surround sound, comfortable for long sessions
   
2. Mechanical Gaming Keyboard RGB - $179.99
   - Responsive keys, customizable lighting
   
Both would make excellent gifts. Would you like more details about either?"
```

---

## 🏗️ Architecture

### System Components

```
┌─────────────────────────────────────────────────────────────┐
│                        User Browser                         │
│  ┌────────────────────────────────────────────────────┐    │
│  │   Copilot Page (Pages/Copilot/Index.cshtml)        │    │
│  │   - Chat UI with message bubbles                   │    │
│  │   - Input form with send button                    │    │
│  │   - Typing indicator                               │    │
│  └─────────────────┬──────────────────────────────────┘    │
└────────────────────┼────────────────────────────────────────┘
                     │ HTTP POST
                     ↓
┌─────────────────────────────────────────────────────────────┐
│                    ASP.NET Core Server                      │
│  ┌────────────────────────────────────────────────────┐    │
│  │   Chat API Endpoint (/api/chat)                    │    │
│  │   - Validates request                              │    │
│  │   - Calls CopilotService                           │    │
│  └─────────────────┬──────────────────────────────────┘    │
│                    ↓                                        │
│  ┌────────────────────────────────────────────────────┐    │
│  │   CopilotService (Services/CopilotService.cs)      │    │
│  │   - Fetches product catalog                        │    │
│  │   - Builds system message with context             │    │
│  │   - Manages conversation history                   │    │
│  │   - Calls Azure OpenAI client                      │    │
│  └─────────────────┬──────────────────────────────────┘    │
└────────────────────┼────────────────────────────────────────┘
                     │ Azure SDK
                     ↓
┌─────────────────────────────────────────────────────────────┐
│                    Azure AI Foundry                         │
│  ┌────────────────────────────────────────────────────┐    │
│  │   GPT-4o Model Deployment                          │    │
│  │   - Processes chat messages                        │    │
│  │   - Generates contextual responses                 │    │
│  │   - Returns AI-generated text                      │    │
│  └────────────────────────────────────────────────────┘    │
└─────────────────────────────────────────────────────────────┘
```

### Key Files

**Backend:**
- `Services/ICopilotService.cs` - Service interface
- `Services/CopilotService.cs` - AI integration logic
- `Program.cs` - API endpoint registration

**Frontend:**
- `Pages/Copilot/Index.cshtml` - Chat UI
- `Pages/Copilot/Index.cshtml.cs` - Page model
- `wwwroot/js/copilot.js` - Client-side logic
- `wwwroot/css/copilot.css` - Styling

**Configuration:**
- `appsettings.json` - Production settings
- `appsettings.Development.json` - Development settings

---

## 🎨 Customization

### Adjust AI Personality

Edit `Services/CopilotService.cs`, method `BuildSystemMessage`:

```csharp
private string BuildSystemMessage(string productContext)
{
    return $@"You are a [YOUR PERSONALITY] retail assistant...
    
Guidelines:
- [Your custom guidelines]
- [Tone and style preferences]
...
    ";
}
```

**Example personalities:**
- Professional and formal
- Casual and friendly
- Enthusiastic and energetic
- Minimalist and concise

### Change Response Parameters

In `appsettings.json`:

```json
{
  "AzureAI": {
    "MaxTokens": 1200,      // Longer responses
    "Temperature": 0.3      // More focused/deterministic
  }
}
```

**Temperature guide:**
- `0.0-0.3`: Very focused, consistent, factual
- `0.4-0.7`: Balanced (recommended for most uses)
- `0.8-1.0`: Very creative, varied, sometimes unexpected

### Customize the UI

**Colors** - Edit `wwwroot/css/copilot.css`:
```css
.user-message {
    background: linear-gradient(135deg, #your-color-1, #your-color-2);
}

.assistant-message {
    background: linear-gradient(135deg, #your-color-3, #your-color-4);
}
```

**Layout** - Edit `Pages/Copilot/Index.cshtml`:
- Change welcome message
- Modify example prompts
- Add custom branding
- Adjust responsive breakpoints

---

## 🐛 Troubleshooting

### Common Issues

#### Issue: "AzureAI:Endpoint must be configured"

**Symptoms:**
- Application fails to start
- Error message on startup

**Solution:**
1. Check `appsettings.Development.json` has the `AzureAI` section
2. Verify the endpoint URL format: `https://your-project.openai.azure.com/`
3. Ensure no typos in the configuration

#### Issue: "DefaultAzureCredential failed to retrieve a token"

**Symptoms:**
- Can't authenticate to Azure
- Error when sending chat messages

**Solution:**
1. Run `az login` in your terminal
2. Verify with `az account show`
3. Check you're logged into the correct subscription
4. Try signing out and back in: `az logout` then `az login`

#### Issue: "Unauthorized" or "Forbidden" (401/403 errors)

**Symptoms:**
- Authentication works but API calls fail
- Error in logs about permissions

**Solution:**
1. Verify the role assignment:
   ```bash
   az role assignment list --assignee your-email@domain.com
   ```
2. Ensure "Cognitive Services OpenAI User" role is assigned
3. Wait 5-10 minutes after role assignment (propagation time)
4. Check the resource scope in the role assignment

#### Issue: "Model deployment not found"

**Symptoms:**
- Error about missing deployment
- 404 errors when calling AI

**Solution:**
1. Verify deployment name in `appsettings.json` matches Azure
2. Check deployment status in Azure AI Foundry (should be "Succeeded")
3. Ensure deployment is not paused or stopped

#### Issue: Chat UI not loading

**Symptoms:**
- Blank page or missing chat interface
- JavaScript errors in browser console

**Solution:**
1. Check browser console (F12) for errors
2. Verify `copilot.js` and `copilot.css` are loading (Network tab)
3. Clear browser cache: Ctrl+Shift+Delete
4. Try a different browser
5. Check if files exist in `wwwroot/js/` and `wwwroot/css/`

#### Issue: Slow responses

**Symptoms:**
- Long wait times for AI responses
- Timeout errors

**Solution:**
1. Check your internet connection speed
2. Verify Azure AI Foundry service health
3. Reduce `MaxTokens` in configuration (smaller = faster)
4. Check Azure subscription quota usage
5. Consider using a faster model (GPT-3.5-turbo)

#### Issue: Build errors

**Symptoms:**
- Can't compile the project
- Missing dependencies

**Solution:**
```bash
# Clean and restore
dotnet clean
dotnet restore

# Rebuild
dotnet build

# If still failing, check for package issues
dotnet list package --vulnerable
dotnet list package --deprecated
```

### Debug Mode

Enable detailed logging in `appsettings.Development.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Information",
      "RetailDecomposed.Services.CopilotService": "Debug"
    }
  }
}
```

---

## 🚀 Production Deployment

### Pre-Deployment Checklist

- [ ] Managed Identity enabled on Azure resource
- [ ] Role assignments configured for Managed Identity
- [ ] Endpoint URL updated in production `appsettings.json`
- [ ] Model deployment is active and stable
- [ ] Error handling and logging configured
- [ ] Application Insights set up for monitoring
- [ ] CORS configured appropriately
- [ ] HTTPS enforced
- [ ] Rate limiting implemented (optional but recommended)

### Step 1: Enable Managed Identity

**For Azure App Service:**
```bash
az webapp identity assign \
  --name <app-service-name> \
  --resource-group <resource-group>
```

**For Azure Container Apps:**
```bash
az containerapp identity assign \
  --name <container-app-name> \
  --resource-group <resource-group>
```

### Step 2: Assign Role to Managed Identity

```bash
# Get the Managed Identity principal ID
PRINCIPAL_ID=$(az webapp identity show \
  --name <app-service-name> \
  --resource-group <resource-group> \
  --query principalId -o tsv)

# Assign the role
az role assignment create \
  --role "Cognitive Services OpenAI User" \
  --assignee $PRINCIPAL_ID \
  --scope /subscriptions/<subscription-id>/resourceGroups/<resource-group>/providers/Microsoft.CognitiveServices/accounts/<ai-resource-name>
```

### Step 3: Update Production Configuration

Update `appsettings.json` (not Development):
```json
{
  "AzureAI": {
    "Endpoint": "https://your-production-project.openai.azure.com/",
    "DeploymentName": "gpt-4o",
    "MaxTokens": 800,
    "Temperature": 0.7
  }
}
```

### Step 4: Deploy Application

```bash
# Example for App Service
az webapp deployment source config-zip \
  --resource-group <resource-group> \
  --name <app-service-name> \
  --src ./publish.zip
```

### Step 5: Verify Deployment

1. Check application logs in Azure Portal
2. Look for: "Initializing Azure AI client with Entra ID authentication"
3. Test the chat functionality
4. Monitor for any errors in Application Insights

### Production Best Practices

1. **Monitoring**: Set up Application Insights alerts for errors
2. **Scaling**: Configure autoscaling based on load
3. **Costs**: Monitor Azure OpenAI token usage and costs
4. **Security**: Implement rate limiting to prevent abuse
5. **Performance**: Consider caching frequently asked questions
6. **Updates**: Keep Azure.AI.OpenAI package up to date

---

## 📝 API Reference

### POST /api/chat

Send a message to the AI assistant and receive a response.

**Endpoint:** `/api/chat`

**Method:** `POST`

**Request Body:**
```json
{
  "message": "What laptops do you have?",
  "conversationHistory": [
    {
      "role": "user",
      "content": "Hello"
    },
    {
      "role": "assistant",
      "content": "Hello! How can I help you today?"
    }
  ]
}
```

**Request Parameters:**

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `message` | string | ✅ Yes | The user's message to the AI |
| `conversationHistory` | array | No | Previous messages for context |
| `conversationHistory[].role` | string | - | Either "user" or "assistant" |
| `conversationHistory[].content` | string | - | The message content |

**Response (Success - 200 OK):**
```json
{
  "response": "We have several great laptop options available:\n\n1. Dell XPS 13 - $1,299.99\n   - Ultra-portable design\n   - 13.4\" display\n\n2. MacBook Air M2 - $1,199.00\n   - Excellent battery life\n   - Silent operation\n\nWhich features are most important to you?"
}
```

**Response (Error - 400 Bad Request):**
```json
{
  "error": "Message is required"
}
```

**Response (Error - 500 Internal Server Error):**
```json
{
  "response": "I apologize, but I'm having trouble processing your request right now. Please try again later."
}
```

**Status Codes:**
- `200 OK` - Successful response
- `400 Bad Request` - Invalid request (missing message)
- `401 Unauthorized` - Authentication required
- `500 Internal Server Error` - Server or AI service error

**Example Usage (JavaScript):**
```javascript
const response = await fetch('/api/chat', {
    method: 'POST',
    headers: {
        'Content-Type': 'application/json'
    },
    body: JSON.stringify({
        message: "What laptops do you have?",
        conversationHistory: [
            { role: "user", content: "Hello" },
            { role: "assistant", content: "Hello! How can I help you?" }
        ]
    })
});

const data = await response.json();
console.log(data.response);
```

---

## 📚 Additional Resources

- [Azure AI Foundry Portal](https://ai.azure.com)
- [Azure OpenAI Documentation](https://learn.microsoft.com/azure/ai-services/openai/)
- [DefaultAzureCredential Reference](https://learn.microsoft.com/dotnet/api/azure.identity.defaultazurecredential)
- [Azure RBAC Documentation](https://learn.microsoft.com/azure/role-based-access-control/)
- [Azure CLI Reference](https://learn.microsoft.com/cli/azure/)
- [.NET Azure SDK](https://azure.github.io/azure-sdk/releases/latest/dotnet.html)

---

## 🤝 Support & Feedback

For issues or questions:
1. Check the [Troubleshooting](#troubleshooting) section
2. Review Azure AI Foundry service health
3. Check application logs
4. Verify configuration settings
5. Consult Azure OpenAI documentation

---

## 📄 Summary

You've successfully set up an AI-powered shopping assistant using:
- ✅ Azure AI Foundry with GPT-4o
- ✅ Entra ID authentication (secure, keyless)
- ✅ ASP.NET Core integration
- ✅ Modern chat UI with conversation history

The AI assistant is now ready to help your customers find products, get recommendations, and answer questions about your catalog!

---

**Version:** 1.0  
**Last Updated:** November 22, 2025
