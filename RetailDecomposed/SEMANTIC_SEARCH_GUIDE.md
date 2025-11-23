# Semantic Search - Complete Implementation Guide

This comprehensive guide covers implementing semantic search using Azure AI Search and Azure OpenAI embeddings with passwordless authentication (Entra ID RBAC).

## Table of Contents

1. [Overview](#overview)
2. [Architecture](#architecture)
3. [Prerequisites](#prerequisites)
4. [Azure Resources Setup](#azure-resources-setup)
5. [RBAC Configuration](#rbac-configuration)
6. [Application Configuration](#application-configuration)
7. [Implementation Details](#implementation-details)
8. [Testing & Verification](#testing--verification)
9. [Production Deployment](#production-deployment)
10. [Troubleshooting](#troubleshooting)
11. [Security Best Practices](#security-best-practices)

---

## Overview

Semantic search enables natural language product discovery by:
- Converting product descriptions into 1536-dimension embeddings
- Storing embeddings in Azure AI Search with vector search capabilities
- Matching user queries with products based on semantic similarity (not just keywords)

**Key Features**:
- ✅ Natural language queries ("comfortable outdoor clothing" instead of exact keywords)
- ✅ Vector similarity search (1536-dimension embeddings)
- ✅ Passwordless authentication (Azure Entra ID RBAC)
- ✅ Zero secrets in code (uses DefaultAzureCredential)
- ✅ Production-ready security

**Example Queries**:
- "comfortable outdoor clothing" → Returns hiking gear, jackets, camping apparel
- "running gear for fitness" → Returns athletic shoes, workout clothes
- "casual summer wear" → Returns t-shirts, shorts, light clothing

---

## Architecture

### High-Level Flow

```
User Query ("comfortable outdoor clothing")
        ↓
SemanticSearchService.SearchAsync()
        ↓
   1. Generate Query Embedding
      → Azure OpenAI (text-embedding-3-small)
      → Returns 1536-dimension vector
        ↓
   2. Vector Search in Azure AI Search
      → Compare query vector with product vectors
      → Cosine similarity ranking
        ↓
   3. Return Top Results
      → Products ranked by similarity score
```

### Authentication Architecture

```
RetailDecomposed App
        ↓
DefaultAzureCredential (automatically selects):
    • ManagedIdentity (production - App Service)
    • AzureCli (local development)
    • VisualStudio (local development)
    • Environment (CI/CD pipelines)
        ↓
   ┌────┴────────┐
   ↓             ↓
Azure AI      Azure AI Search
Foundry       (RBAC enabled!)
(OpenAI)
```

### Components

| Component | Purpose | Technology |
|-----------|---------|------------|
| **SemanticSearchService** | Core search logic | C# service class |
| **Azure OpenAI** | Generate embeddings | text-embedding-3-small (1536d) |
| **Azure AI Search** | Store & query vectors | Vector search, cosine similarity |
| **ProductSearchDocument** | Search index schema | C# model class |
| **Search Page** | User interface | Razor Page (ASP.NET Core) |

---

## Prerequisites

### Required Software
- ✅ .NET 9.0 SDK or later
- ✅ Azure CLI (`az --version` >= 2.50.0)
- ✅ PowerShell 7+ (Windows) or Bash (Linux/Mac)
- ✅ Visual Studio 2022 or VS Code

### Required Azure Resources
- ✅ Azure Subscription with permissions to:
  - Create Azure AI resources
  - Create Azure AI Search service
  - Assign RBAC roles
- ✅ Azure AI Foundry project or Azure OpenAI resource
- ✅ Azure AI Search service
- ✅ Azure Entra ID tenant

### Required Permissions
- Contributor or Owner role on resource group
- User Access Administrator (to assign RBAC roles)

---

## Azure Resources Setup

### Step 1: Create Azure AI Search Service

```bash
# Set variables
$resourceGroup = "rg-retail-app"
$location = "eastus"
$searchServiceName = "retail-search-unique123"  # Must be globally unique

# Create search service (Free tier for testing)
az search service create `
  --name $searchServiceName `
  --resource-group $resourceGroup `
  --location $location `
  --sku free `
  --partition-count 1 `
  --replica-count 1
```

**⚠️ CRITICAL: Enable RBAC Authentication**

By default, Azure Search uses API key authentication only. You MUST enable RBAC:

```bash
az search service update `
  --name $searchServiceName `
  --resource-group $resourceGroup `
  --auth-options aadOrApiKey `
  --aad-auth-failure-mode http401WithBearerChallenge
```

**What this does**:
- Enables Entra ID authentication (RBAC)
- Keeps API key authentication as fallback (recommended)
- Sets proper authentication failure responses

**Verify RBAC is enabled**:
```bash
az search service show `
  --name $searchServiceName `
  --resource-group $resourceGroup `
  --query authOptions
```

Should show:
```json
{
  "aadOrApiKey": {
    "aadAuthFailureMode": "http401WithBearerChallenge"
  }
}
```

### Step 2: Create Azure AI Foundry Resource

**Option A: Using Azure AI Foundry Portal** (Recommended)

1. Navigate to [Azure AI Foundry](https://ai.azure.com)
2. Create new project or use existing
3. Deploy required models:
   - **text-embedding-3-small** (for embeddings, 1536 dimensions)
   - **gpt-4o** (for AI Copilot chat, optional)
4. Note your project endpoint: `https://<project-name>.services.ai.azure.com/`

**Option B: Using Azure OpenAI** (Alternative)

```bash
# Create Azure OpenAI resource
az cognitiveservices account create `
  --name retail-openai-service `
  --resource-group $resourceGroup `
  --location $location `
  --kind OpenAI `
  --sku S0

# Deploy embedding model (requires Azure OpenAI Studio)
# Visit: https://oai.azure.com/portal
```

---

## RBAC Configuration

### Local Development Setup

#### Step 1: Authenticate with Azure CLI

```bash
# Login to Azure with your tenant
az login --tenant <your-tenant-id>

# Verify authentication
az account show

# Set correct subscription (if multiple)
az account set --subscription <your-subscription-id>
```

#### Step 2: Assign RBAC Roles to Your User

You need **three roles** for full semantic search functionality:

```bash
# Get your user object ID
$userId = az ad signed-in-user show --query id -o tsv

# Role 1: Search Index Data Contributor
# Purpose: Upload and read product embeddings to/from search index
az role assignment create `
  --role "Search Index Data Contributor" `
  --assignee $userId `
  --scope "/subscriptions/<subscription-id>/resourceGroups/<rg-name>/providers/Microsoft.Search/searchServices/<search-service-name>"

# Role 2: Search Service Contributor
# Purpose: Create and update search indexes, manage vector search configurations
az role assignment create `
  --role "Search Service Contributor" `
  --assignee $userId `
  --scope "/subscriptions/<subscription-id>/resourceGroups/<rg-name>/providers/Microsoft.Search/searchServices/<search-service-name>"

# Role 3: Cognitive Services OpenAI User
# Purpose: Generate 1536-dimension embeddings via API
az role assignment create `
  --role "Cognitive Services OpenAI User" `
  --assignee $userId `
  --scope "/subscriptions/<subscription-id>/resourceGroups/<rg-name>/providers/Microsoft.CognitiveServices/accounts/<ai-foundry-name>"
```

#### Step 3: Verify Role Assignments

```bash
# Check Azure Search roles
az role assignment list `
  --assignee $userId `
  --scope "/subscriptions/<subscription-id>/resourceGroups/<rg-name>/providers/Microsoft.Search/searchServices/<search-service-name>" `
  --output table

# Check Azure AI roles
az role assignment list `
  --assignee $userId `
  --scope "/subscriptions/<subscription-id>/resourceGroups/<rg-name>/providers/Microsoft.CognitiveServices/accounts/<ai-foundry-name>" `
  --output table
```

### Production Deployment Setup

#### Step 1: Enable Managed Identity on App Service

```bash
# Enable System-Assigned Managed Identity
az webapp identity assign `
  --name <your-app-service-name> `
  --resource-group <your-resource-group>

# Get the managed identity principal ID
$managedIdentityId = az webapp identity show `
  --name <your-app-service-name> `
  --resource-group <your-resource-group> `
  --query principalId -o tsv

echo "Managed Identity Principal ID: $managedIdentityId"
```

#### Step 2: Assign RBAC Roles to Managed Identity

```bash
# Assign all three roles to the managed identity
az role assignment create `
  --role "Search Index Data Contributor" `
  --assignee $managedIdentityId `
  --scope "/subscriptions/<subscription-id>/resourceGroups/<rg-name>/providers/Microsoft.Search/searchServices/<search-service-name>"

az role assignment create `
  --role "Search Service Contributor" `
  --assignee $managedIdentityId `
  --scope "/subscriptions/<subscription-id>/resourceGroups/<rg-name>/providers/Microsoft.Search/searchServices/<search-service-name>"

az role assignment create `
  --role "Cognitive Services OpenAI User" `
  --assignee $managedIdentityId `
  --scope "/subscriptions/<subscription-id>/resourceGroups/<rg-name>/providers/Microsoft.CognitiveServices/accounts/<ai-foundry-name>"
```

#### Step 3: Configure App Service Settings (No Secrets!)

```bash
az webapp config appsettings set `
  --name <your-app-service-name> `
  --resource-group <your-resource-group> `
  --settings `
    AzureAI__Endpoint="https://<your-foundry-project>.services.ai.azure.com/" `
    AzureSearch__Endpoint="https://<your-search-service>.search.windows.net" `
    AzureSearch__IndexName="products-index" `
    AzureSearch__EmbeddingDeploymentName="text-embedding-3-small"
```

### CI/CD Pipeline Setup (Service Principal)

#### Step 1: Create Service Principal

```bash
az ad sp create-for-rbac `
  --name "RetailDecomposed-CI-SP" `
  --role Contributor `
  --scopes "/subscriptions/<subscription-id>/resourceGroups/<rg-name>"

# Note the output:
# - appId (Client ID)
# - password (Client Secret)
# - tenant (Tenant ID)
```

#### Step 2: Assign RBAC Roles to Service Principal

```bash
# Get service principal object ID
$spObjectId = az ad sp show --id <app-id-from-step-1> --query id -o tsv

# Assign the three roles
az role assignment create `
  --role "Search Index Data Contributor" `
  --assignee $spObjectId `
  --scope "/subscriptions/<subscription-id>/resourceGroups/<rg-name>/providers/Microsoft.Search/searchServices/<search-service-name>"

az role assignment create `
  --role "Search Service Contributor" `
  --assignee $spObjectId `
  --scope "/subscriptions/<subscription-id>/resourceGroups/<rg-name>/providers/Microsoft.Search/searchServices/<search-service-name>"

az role assignment create `
  --role "Cognitive Services OpenAI User" `
  --assignee $spObjectId `
  --scope "/subscriptions/<subscription-id>/resourceGroups/<rg-name>/providers/Microsoft.CognitiveServices/accounts/<ai-foundry-name>"
```

#### Step 3: Configure CI/CD Environment Variables

**GitHub Actions**:
```yaml
env:
  AZURE_CLIENT_ID: ${{ secrets.AZURE_CLIENT_ID }}
  AZURE_CLIENT_SECRET: ${{ secrets.AZURE_CLIENT_SECRET }}
  AZURE_TENANT_ID: ${{ secrets.AZURE_TENANT_ID }}
```

**Azure DevOps**:
```yaml
variables:
  - name: AZURE_CLIENT_ID
    value: $(azure-client-id)
  - name: AZURE_CLIENT_SECRET
    value: $(azure-client-secret)
  - name: AZURE_TENANT_ID
    value: $(azure-tenant-id)
```

DefaultAzureCredential will automatically detect and use these environment variables.

---

## Application Configuration

### appsettings.Development.json

```json
{
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

**Important**: No API keys! Authentication uses DefaultAzureCredential.

### Program.cs Configuration

```csharp
// Register SemanticSearchService with dependency injection
builder.Services.AddSingleton<ISemanticSearchService, SemanticSearchService>();

// Configure logging
builder.Services.AddLogging(logging =>
{
    logging.AddConsole();
    logging.AddDebug();
});
```

---

## Implementation Details

### 1. Search Index Schema (ProductSearchDocument.cs)

```csharp
public class ProductSearchDocument
{
    [SimpleField(IsKey = true, IsFilterable = true)]
    public string Id { get; set; } = string.Empty;

    [SearchableField(IsFilterable = true, IsSortable = true)]
    public string Sku { get; set; } = string.Empty;

    [SearchableField(IsFilterable = true, IsSortable = true)]
    public string Name { get; set; } = string.Empty;

    [SearchableField]
    public string Description { get; set; } = string.Empty;

    [SearchableField(IsFilterable = true, IsFacetable = true)]
    public string Category { get; set; } = string.Empty;

    [SimpleField(IsFilterable = true, IsSortable = true)]
    public decimal Price { get; set; }

    // 1536-dimension embedding vector
    [VectorSearchField(VectorSearchDimensions = 1536, VectorSearchProfileName = "vector-profile")]
    public ReadOnlyMemory<float> NameDescriptionVector { get; set; }
}
```

### 2. Vector Search Configuration

```csharp
// Vector search algorithm configuration
var vectorSearch = new VectorSearch
{
    Algorithms =
    {
        new HnswAlgorithmConfiguration("hnsw-algorithm")
        {
            Parameters = new HnswParameters
            {
                Metric = VectorSearchAlgorithmMetric.Cosine,
                M = 4,
                EfConstruction = 400,
                EfSearch = 500
            }
        }
    },
    Profiles =
    {
        new VectorSearchProfile("vector-profile", "hnsw-algorithm")
    }
};
```

**Parameters Explained**:
- **Metric**: Cosine similarity (measures angle between vectors)
- **M**: Graph connectivity (higher = better recall, more memory)
- **EfConstruction**: Index build quality (higher = better quality, slower build)
- **EfSearch**: Search quality (higher = better recall, slower search)

### 3. Embedding Generation

```csharp
public async Task<ReadOnlyMemory<float>> GenerateEmbeddingAsync(string text)
{
    var embeddingOptions = new EmbeddingsOptions(_embeddingDeploymentName, new[] { text });
    var response = await _openAIClient.GetEmbeddingsAsync(embeddingOptions);
    
    return response.Value.Data[0].Embedding;
}
```

### 4. Vector Search Query

```csharp
public async Task<List<ProductSearchResult>> SearchAsync(string query, int topK = 10)
{
    // 1. Generate query embedding
    var queryEmbedding = await GenerateEmbeddingAsync(query);
    
    // 2. Configure vector search query
    var vectorQuery = new VectorizedQuery(queryEmbedding)
    {
        KNearestNeighborsCount = topK,
        Fields = { "NameDescriptionVector" }
    };
    
    // 3. Execute search
    var searchOptions = new SearchOptions
    {
        VectorSearch = new() { Queries = { vectorQuery } },
        Size = topK
    };
    
    var response = await _searchClient.SearchAsync<ProductSearchDocument>(null, searchOptions);
    
    // 4. Return results with similarity scores
    var results = new List<ProductSearchResult>();
    await foreach (var result in response.Value.GetResultsAsync())
    {
        results.Add(new ProductSearchResult
        {
            Product = MapToProduct(result.Document),
            Score = result.Score ?? 0,
            SearchScore = result.Score ?? 0
        });
    }
    
    return results;
}
```

---

## Testing & Verification

### Test 1: Verify Authentication

```bash
# Run the application
cd RetailDecomposed
dotnet run

# Check logs for successful authentication
# Expected: "Successfully created SearchClient with Entra ID authentication"
# Expected: "Successfully created AzureOpenAIClient with Entra ID authentication"
# NOT Expected: 403 Forbidden errors
```

### Test 2: Create Search Index (Admin Only)

1. Navigate to `https://localhost:6068/Search`
2. Sign in as Admin user
3. Click **"Create Index"** button
4. Expected result: "Index created successfully!"
5. Verify in Azure Portal:
   - Go to Azure Search service
   - Check **Indexes** section
   - Should see `products-index` with vector search configuration

### Test 3: Index Products (Admin Only)

1. On Search page, click **"Index Products"** button
2. Expected result: "Successfully indexed 60 products with embeddings"
3. Monitor logs:
   ```
   Generating embeddings for 60 products...
   Processing product 1/60: Product Name
   Processing product 2/60: Product Name
   ...
   Successfully indexed 60 products
   ```
4. Verify in Azure Portal:
   - Go to products-index
   - Check **Document Count**: should be 60
   - Check **Storage Size**: should show vector data

### Test 4: Perform Semantic Search

**Test Query 1: "comfortable outdoor clothing"**
1. Enter query in search box
2. Click **Search**
3. Expected results:
   - Hiking jackets
   - Camping gear
   - Outdoor apparel
   - Products ranked by relevance (similarity score)

**Test Query 2: "running gear for fitness"**
1. Enter query
2. Expected results:
   - Running shoes
   - Athletic wear
   - Fitness equipment

**Test Query 3: "casual summer wear"**
1. Enter query
2. Expected results:
   - T-shirts
   - Shorts
   - Light clothing

### Test 5: Verify Similarity Scores

Check that results have meaningful similarity scores:
- Score 0.8-1.0: Highly relevant
- Score 0.6-0.8: Relevant
- Score 0.4-0.6: Somewhat relevant
- Score < 0.4: Low relevance

### Test 6: API Endpoint Testing

```bash
# Using curl (PowerShell)
$query = "comfortable outdoor clothing"
Invoke-RestMethod -Uri "https://localhost:6068/api/search?query=$query" -Method Get

# Expected JSON response
{
  "results": [
    {
      "id": "1",
      "name": "Hiking Jacket",
      "description": "Comfortable waterproof jacket...",
      "score": 0.89
    },
    ...
  ],
  "totalCount": 10
}
```

---

## Production Deployment

### Deployment Checklist

- [ ] Azure AI Search service created with Free or Basic SKU
- [ ] RBAC authentication enabled on Azure Search (`aadOrApiKey`)
- [ ] Azure AI Foundry project created with embedding model deployed
- [ ] App Service created with Managed Identity enabled
- [ ] Three RBAC roles assigned to Managed Identity
- [ ] App Service configuration settings updated (no secrets)
- [ ] Application deployed to App Service
- [ ] Search index created (admin action)
- [ ] Products indexed with embeddings (admin action)
- [ ] End-to-end testing completed

### Production Configuration

**App Service Settings** (Azure Portal → Configuration):
```
AzureAI__Endpoint = https://<project>.services.ai.azure.com/
AzureSearch__Endpoint = https://<search-service>.search.windows.net
AzureSearch__IndexName = products-index
AzureSearch__EmbeddingDeploymentName = text-embedding-3-small
```

**No secrets needed!** Managed Identity handles authentication.

### Monitoring & Logging

Enable Application Insights for production monitoring:

```bash
# Create Application Insights
az monitor app-insights component create `
  --app retail-app-insights `
  --resource-group $resourceGroup `
  --location $location

# Link to App Service
az webapp config appsettings set `
  --name <app-service-name> `
  --resource-group $resourceGroup `
  --settings APPINSIGHTS_INSTRUMENTATIONKEY=<instrumentation-key>
```

Monitor:
- Search query performance
- Embedding generation latency
- Authentication failures
- API response times

---

## Troubleshooting

### Issue: 403 Forbidden when accessing Azure Search

**Root Cause**: Azure Search has RBAC disabled (apiKeyOnly mode)

**Solution**:
```bash
az search service update `
  --name <search-service-name> `
  --resource-group <resource-group> `
  --auth-options aadOrApiKey `
  --aad-auth-failure-mode http401WithBearerChallenge
```

**Verify**:
```bash
az search service show `
  --name <search-service-name> `
  --resource-group <resource-group> `
  --query authOptions
```

### Issue: "InteractiveBrowserCredential authentication failed"

**Root Cause**: DefaultAzureCredential trying browser authentication

**Solution**: Authenticate with Azure CLI:
```bash
az login --tenant <tenant-id>
az account set --subscription <subscription-id>
```

### Issue: Role assignments not taking effect

**Solution**:
1. Wait 5-10 minutes for RBAC propagation
2. Re-authenticate: `az logout` then `az login`
3. Verify roles: `az role assignment list --assignee <object-id>`
4. Check service authentication mode: `az search service show`

### Issue: Embeddings generation fails

**Root Cause**: Missing "Cognitive Services OpenAI User" role

**Solution**:
```bash
az role assignment create `
  --role "Cognitive Services OpenAI User" `
  --assignee <object-id> `
  --scope "<ai-resource-scope>"
```

### Issue: Search returns no results

**Troubleshooting Steps**:
1. Verify index exists: Check Azure Portal → Azure Search → Indexes
2. Check document count: Should match product count (e.g., 60)
3. Verify embeddings: Check storage size (vectors should add ~6KB per product)
4. Test with simple query: "shoe" or "shirt"
5. Check logs for errors during indexing

### Issue: Works locally but fails in App Service

**Solution**:
1. Verify Managed Identity is enabled
2. Check RBAC roles assigned to **managed identity** (not your user)
3. Verify App Service configuration (no secrets, correct endpoints)
4. Check App Service logs: `az webapp log tail`
5. Wait for RBAC propagation (5-10 minutes after role assignment)

---

## Security Best Practices

### 1. Authentication & Authorization

- ✅ **Use Managed Identity in production** - no secrets to rotate
- ✅ **Use Azure CLI locally** - leverages existing authentication
- ✅ **Resource-level RBAC** - assign roles to specific resources, not entire subscriptions
- ✅ **Principle of least privilege** - only assign required roles
- ❌ **Never commit API keys** - use RBAC exclusively

### 2. Role Scopes

**Resource-Level** (✅ Recommended):
```bash
--scope "/subscriptions/<sub-id>/resourceGroups/<rg>/providers/Microsoft.Search/searchServices/<service>"
```

**Resource Group-Level** (⚠️ Caution):
```bash
--scope "/subscriptions/<sub-id>/resourceGroups/<rg>"
# Grants access to ALL resources in the group
```

**Subscription-Level** (❌ Not Recommended):
```bash
--scope "/subscriptions/<sub-id>"
# Too broad - avoid in production
```

### 3. Network Security

Consider implementing:
- Private Endpoints for Azure Search
- VNet integration for App Service
- Azure Firewall rules
- Deny public access, allow only from VNet

### 4. Monitoring & Auditing

- Enable Azure Monitor logs
- Track authentication events
- Monitor search query patterns
- Set up alerts for authentication failures
- Regular RBAC access reviews

### 5. Data Protection

- Use HTTPS only (enforced by default)
- Implement rate limiting for search API
- Sanitize user input to prevent injection attacks
- Consider data encryption at rest (enabled by default in Azure Search)

---

## Performance Optimization

### Embedding Generation

- **Batch processing**: Generate embeddings for multiple products in parallel
- **Caching**: Cache embeddings to avoid regeneration
- **Retry logic**: Implement exponential backoff for transient failures

### Search Performance

- **Tune HNSW parameters**:
  - Increase `EfSearch` for better recall (slower)
  - Decrease for faster search (lower recall)
- **Use filters**: Combine vector search with filters (category, price range)
- **Pagination**: Implement for large result sets

### Cost Optimization

- **Azure Search Free tier**: Good for testing (50MB storage, 10K documents)
- **Azure Search Basic tier**: Production workloads (~$75/month)
- **Embedding generation**: ~$0.0001 per 1K tokens
- **Consider caching**: Reduce embedding API calls

---

## Additional Resources

### Microsoft Documentation
- [Azure AI Search Documentation](https://learn.microsoft.com/azure/search/)
- [Azure AI Search Vector Search](https://learn.microsoft.com/azure/search/vector-search-overview)
- [Azure OpenAI Embeddings](https://learn.microsoft.com/azure/ai-services/openai/concepts/embeddings)
- [Azure RBAC Documentation](https://learn.microsoft.com/azure/role-based-access-control/)
- [DefaultAzureCredential](https://learn.microsoft.com/dotnet/api/azure.identity.defaultazurecredential)

### Related Project Documentation
- [README.md](README.md) - Project overview and getting started
- [AUTHENTICATION_SETUP.md](AUTHENTICATION_SETUP.md) - Azure Entra ID user authentication
- [DEPLOYMENT_GUIDE.md](DEPLOYMENT_GUIDE.md) - Production deployment guide
- [AI_COPILOT_COMPLETE_GUIDE.md](AI_COPILOT_COMPLETE_GUIDE.md) - AI Copilot feature

---

## Summary

### Critical Success Factors

1. ✅ **Enable RBAC on Azure Search** - Most common issue!
   ```bash
   az search service update --auth-options aadOrApiKey
   ```

2. ✅ **Assign three RBAC roles**:
   - Search Index Data Contributor
   - Search Service Contributor
   - Cognitive Services OpenAI User

3. ✅ **Use DefaultAzureCredential** - Works in all environments

4. ✅ **No secrets in code** - Managed Identity (prod) + Azure CLI (local)

5. ✅ **Test thoroughly** - Index creation, product indexing, search queries

### Common Pitfalls to Avoid

- ❌ Forgetting to enable RBAC on Azure Search service
- ❌ Assigning roles at wrong scope level
- ❌ Not waiting for RBAC propagation (5-10 minutes)
- ❌ Using wrong Azure Search authentication mode
- ❌ Mixing API keys with RBAC authentication
- ❌ Not testing locally before deploying to production

### Quick Reference Commands

```bash
# Enable RBAC on Azure Search
az search service update --name <service> --resource-group <rg> --auth-options aadOrApiKey

# Assign roles (repeat for all 3 roles)
az role assignment create --role "<role-name>" --assignee <object-id> --scope "<scope>"

# Verify roles
az role assignment list --assignee <object-id> --output table

# Authenticate locally
az login --tenant <tenant-id>
```

---

**Last Updated**: November 23, 2025  
**Tested With**: Azure CLI 2.66.0, .NET 9.0, Azure.Search.Documents 11.7.0, Azure.AI.OpenAI 2.1.0

**Need Help?** Check the [Troubleshooting](#troubleshooting) section or review application logs.
