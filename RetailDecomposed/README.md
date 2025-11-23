# Retail Decomposed - Modernized E-Commerce Application

A modernized retail application demonstrating decomposed microservices architecture with AI-powered features including semantic search and intelligent chatbot assistance.

## 📚 Documentation

- **[SEMANTIC_SEARCH_GUIDE.md](SEMANTIC_SEARCH_GUIDE.md)** - Complete semantic search implementation guide (RBAC, setup, deployment)
- **[AUTHENTICATION_SETUP.md](AUTHENTICATION_SETUP.md)** - Azure Entra ID user authentication setup
- **[DEPLOYMENT_GUIDE.md](DEPLOYMENT_GUIDE.md)** - Production deployment to Azure
- **[AI_COPILOT_COMPLETE_GUIDE.md](AI_COPILOT_COMPLETE_GUIDE.md)** - AI Copilot feature guide
- **[TESTING_CHECKLIST.md](TESTING_CHECKLIST.md)** - Comprehensive testing guide

## 🏗️ Architecture Overview

This application represents a decomposed version of the monolithic retail application, featuring:

- **Decomposed API Architecture**: Separate API endpoints for Products, Cart, Orders, and Checkout
- **AI-Powered Search**: Semantic search using Azure AI Search and OpenAI embeddings
- **Intelligent Copilot**: AI assistant powered by Azure OpenAI
- **Secure Authentication**: Azure Entra ID (AAD) with role-based access control
- **Modern UI**: ASP.NET Core Razor Pages with Bootstrap 5

## 🚀 Features

### Core E-Commerce Functionality
- Product catalog browsing with categories
- Shopping cart management
- Order placement and tracking
- Payment processing (mock gateway)

### AI-Powered Features
- **Semantic Search**: Natural language product discovery using embeddings
- **AI Copilot**: Conversational assistant for shopping guidance
- **Vector Search**: 1536-dimension embeddings for accurate similarity matching

### Security & Authentication
- Azure Entra ID authentication
- Role-based access control (Admin/User)
- Secure API endpoints
- Zero secrets in code (uses Managed Identity/DefaultAzureCredential)

## 📋 Prerequisites

- .NET 9.0 SDK
- SQL Server or LocalDB
- Azure Subscription with:
  - Azure AI Foundry (or Azure OpenAI)
  - Azure AI Search service
  - Azure Entra ID tenant

## 🔧 Configuration

### Required Azure Resources

1. **Azure AI Foundry / OpenAI**
   - Deployment: `text-embedding-3-small` (1536 dimensions)
   - Model: `gpt-4o` for chat

2. **Azure AI Search**
   - SKU: Free or Basic
   - Authentication: Entra ID enabled (`aadOrApiKey`)

3. **Azure Entra ID**
   - App registration (for local development)
   - Service principal (for production deployment)

### appsettings.Development.json

```json
{
  "AzureAd": {
    "Instance": "https://login.microsoftonline.com/",
    "TenantId": "<your-tenant-id>",
    "ClientId": "<your-app-registration-client-id>",
    "Domain": "<your-tenant-domain>",
    "CallbackPath": "/signin-oidc"
  },
  "AzureAI": {
    "Endpoint": "https://<your-foundry-project>.services.ai.azure.com/",
    "ChatDeploymentName": "gpt-4o",
    "ChatModelName": "gpt-4o"
  },
  "AzureSearch": {
    "Endpoint": "https://<your-search-service>.search.windows.net",
    "IndexName": "products-index",
    "EmbeddingDeploymentName": "text-embedding-3-small"
  },
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=RetailDecomposed;Trusted_Connection=True;MultipleActiveResultSets=true"
  }
}
```

## 🔐 Authentication Setup

### Local Development

1. **Azure CLI Authentication**
   ```bash
   az login --tenant <your-tenant-id>
   ```

2. **Required RBAC Roles** (for your user):
   - `Search Index Data Contributor` (Azure Search)
   - `Search Service Contributor` (Azure Search)
   - `Cognitive Services OpenAI User` (Azure AI Foundry)

### Production Deployment

1. **Enable Managed Identity** on App Service/Container App

2. **Assign RBAC Roles** (for service principal):
   - `Search Index Data Contributor`
   - `Search Service Contributor`
   - `Cognitive Services OpenAI User`

3. **No secrets needed** - DefaultAzureCredential handles authentication

## 🏃 Running the Application

### First-Time Setup

```bash
# Navigate to project directory
cd RetailDecomposed

# Restore packages
dotnet restore

# Run database migrations (if any)
dotnet ef database update

# Run the application
dotnet run
```

Application will be available at:
- HTTPS: https://localhost:6068
- HTTP: http://localhost:6067

### Initialize Semantic Search

1. Navigate to **Search** page
2. Click **"Create Index"** (Admin only)
3. Click **"Index Products"** (Admin only)
4. Search is now ready!

## 📁 Project Structure

```
RetailDecomposed/
├── Controllers/
│   └── CopilotController.cs          # AI chat API endpoint
├── Data/
│   ├── AppDbContext.cs                # Entity Framework context
│   └── DesignTimeDbContextFactory.cs  # EF design-time support
├── Models/
│   ├── Product.cs                     # Product entity
│   ├── Cart.cs                        # Shopping cart
│   ├── Order.cs                       # Order entity
│   └── ProductSearchDocument.cs       # Search index schema
├── Pages/
│   ├── Index.cshtml                   # Home page
│   ├── Products/Index.cshtml          # Product catalog
│   ├── Cart/Index.cshtml              # Shopping cart
│   ├── Checkout/Index.cshtml          # Checkout process
│   ├── Orders/                        # Order management
│   ├── Search/Index.cshtml            # Semantic search UI
│   └── Copilot/Index.cshtml          # AI assistant chat
├── Services/
│   ├── CopilotService.cs              # AI chat service
│   ├── SemanticSearchService.cs       # Search & embeddings
│   ├── ProductsApiClient.cs           # Products module client
│   ├── CartApiClient.cs               # Cart module client
│   └── OrdersApiClient.cs             # Orders module client
└── wwwroot/
    ├── css/                           # Styles
    └── js/                            # JavaScript

```

## 🔌 API Endpoints

### Products API
- `GET /api/products` - List all active products
- `GET /api/products/{id}` - Get product by ID

### Cart API
- `GET /api/cart/{customerId}` - Get customer cart
- `POST /api/cart/{customerId}/items` - Add item to cart

### Orders API
- `GET /api/orders` - List all orders
- `GET /api/orders/{id}` - Get order by ID

### Checkout API
- `POST /api/checkout` - Process checkout

### AI Copilot API
- `POST /api/copilot/chat` - Chat with AI assistant

### Semantic Search API
- `POST /api/search/create-index` - Create search index (Admin)
- `POST /api/search/index` - Index all products (Admin)
- `GET /api/search?query={q}` - Search products
- `GET /api/search/categories` - Get product categories

## 🧪 Testing

### Manual Testing Checklist

- [ ] User can browse products
- [ ] User can add items to cart
- [ ] User can checkout and place order
- [ ] Admin can create search index
- [ ] Admin can index products
- [ ] Semantic search returns relevant results
- [ ] AI Copilot provides shopping assistance

### Sample Search Queries

- "comfortable outdoor clothing"
- "running gear for fitness"
- "camping equipment"
- "casual summer wear"

## 📚 Key Technologies

- **Backend**: ASP.NET Core 9.0, C# 12
- **Database**: SQL Server with Entity Framework Core
- **Authentication**: Azure Entra ID, ASP.NET Core Identity
- **AI Services**: Azure AI Foundry, Azure OpenAI, Azure AI Search
- **Frontend**: Razor Pages, Bootstrap 5, JavaScript
- **Deployment**: Azure App Service / Container Apps ready

## 🔒 Security Best Practices

1. **No Secrets in Code**: Uses DefaultAzureCredential
2. **RBAC-Based Access**: Fine-grained permissions
3. **Secure by Default**: HTTPS enforced
4. **Role-Based UI**: Admin features restricted
5. **Input Validation**: All user inputs validated

## 📈 Performance Considerations

- **Embedding Caching**: Consider caching embeddings for frequently searched terms
- **Search Pagination**: Implement for large result sets
- **Connection Pooling**: Enabled by default in EF Core
- **Async/Await**: Used throughout for scalability

## 🚢 Deployment

### Azure App Service

```bash
# Publish application
dotnet publish -c Release

# Deploy to Azure (using Azure CLI)
az webapp up --name <your-app-name> --resource-group <your-rg>
```

### Configuration

1. Enable Managed Identity
2. Add RBAC role assignments
3. Configure App Settings (no secrets needed!)
4. Set up Application Insights (optional)

## 🐛 Troubleshooting

### Authentication Issues

**Problem**: 403 Forbidden when accessing Azure Search

**Solution**:
1. Verify Azure Search has RBAC enabled:
   ```bash
   az search service update --name <service-name> --resource-group <rg> \
     --auth-options aadOrApiKey --aad-auth-failure-mode http401WithBearerChallenge
   ```
2. Check RBAC role assignments
3. Re-authenticate: `az login --tenant <tenant-id>`

### Search Not Working

**Problem**: No search results returned

**Solution**:
1. Verify index created: Check Azure Portal
2. Verify products indexed: Check document count in portal
3. Check embeddings: Should be 1536 dimensions
4. Review application logs

### Database Issues

**Problem**: Database connection fails

**Solution**:
1. Verify connection string
2. Run migrations: `dotnet ef database update`
3. Check SQL Server/LocalDB running

## 📖 Related Documentation

- [Azure AI Search Documentation](https://learn.microsoft.com/azure/search/)
- [Azure OpenAI Documentation](https://learn.microsoft.com/azure/ai-services/openai/)
- [Azure Entra ID Documentation](https://learn.microsoft.com/entra/identity/)
- [ASP.NET Core Documentation](https://learn.microsoft.com/aspnet/core/)

## 🤝 Contributing

This is a learning/demo project. Feel free to:
- Report issues
- Suggest improvements
- Submit pull requests

## 📝 License

See LICENSE file in repository root.

---

**Built with ❤️ using Azure AI Services**
