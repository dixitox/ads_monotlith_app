using Azure;
using Azure.AI.OpenAI;
using Azure.Identity;
using OpenAI.Chat;
using RetailMonolith.Models;
using System.Text;

namespace RetailDecomposed.Services
{
    public class CopilotService : ICopilotService
    {
        private readonly AzureOpenAIClient _openAIClient;
        private readonly string _deploymentName;
        private readonly int _maxTokens;
        private readonly float _temperature;
        private readonly IProductsApiClient _productsApiClient;
        private readonly ILogger<CopilotService> _logger;

        public CopilotService(
            IConfiguration configuration,
            IProductsApiClient productsApiClient,
            ILogger<CopilotService> logger)
        {
            var endpoint = configuration["AzureAI:Endpoint"]
                ?? throw new InvalidOperationException("AzureAI:Endpoint must be configured");
            
            var tenantId = configuration["AzureAI:TenantId"];
            
            _deploymentName = configuration["AzureAI:DeploymentName"] ?? "gpt-4o";
            _maxTokens = int.Parse(configuration["AzureAI:MaxTokens"] ?? "800");
            _temperature = float.Parse(configuration["AzureAI:Temperature"] ?? "0.7");

            // Assign dependencies first
            _productsApiClient = productsApiClient;
            _logger = logger;

            // Initialize client with Entra ID authentication with specified tenant
            _logger.LogInformation("Initializing Azure AI client with Entra ID authentication (Tenant: {TenantId})", tenantId ?? "default");
            
            var credentialOptions = new DefaultAzureCredentialOptions();
            if (!string.IsNullOrEmpty(tenantId))
            {
                credentialOptions.TenantId = tenantId;
            }
            
            _openAIClient = new AzureOpenAIClient(new Uri(endpoint), new DefaultAzureCredential(credentialOptions));
        }

        public async Task<string> GetChatResponseAsync(
            string userMessage,
            List<ChatMessage>? conversationHistory = null,
            CancellationToken ct = default)
        {
            try
            {
                // Get product catalog for context
                var products = await _productsApiClient.GetProductsAsync(ct);
                var productContext = BuildProductContext(products);

                // Build the system message with product context
                var systemMessage = BuildSystemMessage(productContext);

                // Create chat messages
                var messages = new List<OpenAI.Chat.ChatMessage>
                {
                    new SystemChatMessage(systemMessage)
                };

                // Add conversation history if provided
                if (conversationHistory != null)
                {
                    foreach (var msg in conversationHistory)
                    {
                        if (msg.Role.ToLower() == "user")
                        {
                            messages.Add(new UserChatMessage(msg.Content));
                        }
                        else if (msg.Role.ToLower() == "assistant")
                        {
                            messages.Add(new AssistantChatMessage(msg.Content));
                        }
                    }
                }

                // Add the current user message
                messages.Add(new UserChatMessage(userMessage));

                // Get chat client
                var chatClient = _openAIClient.GetChatClient(_deploymentName);

                // Create chat completion options
                var chatOptions = new ChatCompletionOptions
                {
                    MaxOutputTokenCount = _maxTokens,
                    Temperature = _temperature
                };

                // Get completion
                var completion = await chatClient.CompleteChatAsync(messages, chatOptions, ct);

                var response = completion.Value.Content[0].Text;
                _logger.LogInformation("AI Copilot response generated for user message: {Message}", userMessage);
                
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating AI copilot response");
                return "I apologize, but I'm having trouble processing your request right now. Please try again later.";
            }
        }

        private string BuildProductContext(IList<Product> products)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Available Products in our catalog:");
            sb.AppendLine();

            foreach (var product in products)
            {
                sb.AppendLine($"- {product.Name} (SKU: {product.Sku})");
                sb.AppendLine($"  Price: {product.Price:C} {product.Currency}");
                if (!string.IsNullOrEmpty(product.Description))
                {
                    sb.AppendLine($"  Description: {product.Description}");
                }
                if (!string.IsNullOrEmpty(product.Category))
                {
                    sb.AppendLine($"  Category: {product.Category}");
                }
                sb.AppendLine();
            }

            return sb.ToString();
        }

        private string BuildSystemMessage(string productContext)
        {
            return $@"You are a friendly and helpful retail shopping assistant for an online store. Your role is to:

1. Help customers find the right products based on their needs and preferences
2. Provide detailed product information including prices, descriptions, and categories
3. Make personalized recommendations based on customer questions
4. Answer questions about our product catalog
5. Help customers add products to cart and place orders
6. Be enthusiastic and conversational while remaining professional

Current Product Catalog:
{productContext}

Guidelines:
- Always be friendly, helpful, and concise
- If a customer asks about a product not in our catalog, politely let them know and suggest similar alternatives if available
- When recommending products, explain why they're a good fit
- Include prices when discussing products
- If you're unsure about something, be honest and offer to help in another way
- Keep responses focused and conversational - avoid overly long explanations unless asked

Interactive Features:
- When mentioning a specific product, ALWAYS add [PRODUCT:id:ProductName] to create a clickable link
- ALWAYS add [ADD_TO_CART:id:ProductName] button after EVERY product mention so customers can add to cart easily
- You can also add [PLACE_ORDER:id:ProductName] to create a ""Place Order"" button
- When recommending multiple products and want to offer bulk actions, use:
  * [ADD_ALL_TO_CART:id1,id2,id3] to add all products to cart at once
  * [ORDER_ALL:id1,id2,id3] to place order for all products at once
- CRITICAL: Every product reference MUST include both [PRODUCT:id:ProductName] and [ADD_TO_CART:id:ProductName]
- Use these features in EVERY response that mentions products
- Format: ""Check out [PRODUCT:5:Product Name] [ADD_TO_CART:5:Product Name]""

Examples:
- ""I recommend the [PRODUCT:5:Dell XPS 13] [ADD_TO_CART:5:Dell XPS 13] - it's perfect for your needs!""
- ""The [PRODUCT:3:MacBook Air] [ADD_TO_CART:3:MacBook Air] is on sale for £1,199.""
- ""Check out this [PRODUCT:8:Gaming Headset Pro] [ADD_TO_CART:8:Gaming Headset Pro] - great reviews!""
- ""We have the [PRODUCT:12:iPhone 14] [ADD_TO_CART:12:iPhone 14] for £799 and [PRODUCT:15:Samsung Galaxy S23] [ADD_TO_CART:15:Samsung Galaxy S23] for £699.""
- ""Here are 3 great options: [PRODUCT:5:Dell XPS 13] [ADD_TO_CART:5:Dell XPS 13], [PRODUCT:8:MacBook Pro] [ADD_TO_CART:8:MacBook Pro], [PRODUCT:12:Surface Laptop] [ADD_TO_CART:12:Surface Laptop]. Or [ADD_ALL_TO_CART:5,8,12] [ORDER_ALL:5,8,12]""
- ""Found 2 beauty products: [PRODUCT:20:Face Cream] [ADD_TO_CART:20:Face Cream] and [PRODUCT:25:Shampoo] [ADD_TO_CART:25:Shampoo]""

IMPORTANT: 
- Always use the format [PRODUCT:id:ProductName], [ADD_TO_CART:id:ProductName], and [PLACE_ORDER:id:ProductName] with actual product names
- When recommending 2+ products, offer bulk actions with [ADD_ALL_TO_CART:id1,id2,...] and [ORDER_ALL:id1,id2,...]\n- For bulk actions, use comma-separated product IDs";
        }
    }
}
