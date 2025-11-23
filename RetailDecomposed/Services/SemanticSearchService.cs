using Azure;
using Azure.AI.OpenAI;
using Azure.Identity;
using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;
using Azure.Search.Documents.Models;
using Microsoft.EntityFrameworkCore;
using RetailDecomposed.Constants;
using RetailDecomposed.Models;
using RetailDecomposed.Data;

namespace RetailDecomposed.Services;

/// <summary>
/// Implementation of semantic search using Azure AI Search and Azure OpenAI embeddings.
/// </summary>
public class SemanticSearchService : ISemanticSearchService
{
    private readonly SearchIndexClient _indexClient;
    private readonly SearchClient _searchClient;
    private readonly AzureOpenAIClient _openAIClient;
    private readonly AppDbContext _dbContext;
    private readonly IConfiguration _configuration;
    private readonly ILogger<SemanticSearchService> _logger;
    private readonly string _indexName;
    private readonly string _embeddingDeploymentName;

    public SemanticSearchService(
        IConfiguration configuration,
        AppDbContext dbContext,
        ILogger<SemanticSearchService> logger)
    {
        _configuration = configuration;
        _dbContext = dbContext;
        _logger = logger;

        // Get configuration
        var searchEndpoint = configuration["AzureSearch:Endpoint"] 
            ?? throw new InvalidOperationException("AzureSearch:Endpoint not configured");
        _indexName = configuration["AzureSearch:IndexName"] 
            ?? throw new InvalidOperationException("AzureSearch:IndexName not configured");
        _embeddingDeploymentName = configuration["AzureSearch:EmbeddingDeploymentName"] 
            ?? throw new InvalidOperationException("AzureSearch:EmbeddingDeploymentName not configured");
        
        var openAIEndpoint = configuration["AzureAI:Endpoint"] 
            ?? throw new InvalidOperationException("AzureAI:Endpoint not configured");
        var tenantId = configuration["AzureAd:TenantId"];

        // Create credential with tenant-specific configuration
        // DefaultAzureCredential tries multiple auth methods: Managed Identity (production), Azure CLI (local dev), etc.
        var credentialOptions = new DefaultAzureCredentialOptions
        {
            TenantId = tenantId,
            ExcludeSharedTokenCacheCredential = true,
            ExcludeVisualStudioCodeCredential = true,
            ExcludeAzurePowerShellCredential = true,
            ExcludeInteractiveBrowserCredential = true
        };
        var credential = new DefaultAzureCredential(credentialOptions);

        // Initialize Azure AI Search clients
        _indexClient = new SearchIndexClient(new Uri(searchEndpoint), credential);
        _searchClient = _indexClient.GetSearchClient(_indexName);

        // Initialize Azure OpenAI client
        _openAIClient = new AzureOpenAIClient(new Uri(openAIEndpoint), credential);

        _logger.LogInformation("SemanticSearchService initialized with Entra ID authentication");
    }

    public async Task<bool> CreateOrUpdateIndexAsync()
    {
        try
        {
            _logger.LogInformation("Creating search index: {IndexName}", _indexName);
            
            // Define vector search configuration
            var vectorSearch = new VectorSearch();
            vectorSearch.Profiles.Add(new VectorSearchProfile("vector-profile", "vector-config"));
            vectorSearch.Algorithms.Add(new HnswAlgorithmConfiguration("vector-config"));

            // Create the search index
            var searchIndex = new SearchIndex(_indexName)
            {
                Fields =
                {
                    new SimpleField("Id", SearchFieldDataType.String) { IsKey = true, IsFilterable = true },
                    new SimpleField("Sku", SearchFieldDataType.String) { IsFilterable = true, IsSortable = true },
                    new SearchableField("Name") { IsFilterable = true, IsSortable = true },
                    new SearchableField("Description") { AnalyzerName = LexicalAnalyzerName.EnMicrosoft },
                    new SearchableField("Category") { IsFilterable = true, IsFacetable = true },
                    new SimpleField("Price", SearchFieldDataType.Double) { IsFilterable = true, IsSortable = true, IsFacetable = true },
                    new VectorSearchField("NameDescriptionVector", 1536, "vector-profile")
                },
                VectorSearch = vectorSearch
            };

            await _indexClient.CreateOrUpdateIndexAsync(searchIndex);
            _logger.LogInformation("Search index '{IndexName}' created successfully", _indexName);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating search index");
            return false;
        }
    }

    public async Task<int> IndexProductsAsync()
    {
        try
        {
            _logger.LogInformation("Starting product indexing process");

            // Get all products from database
            var products = await _dbContext.Products.AsNoTracking().ToListAsync();
            _logger.LogInformation("Retrieved {Count} products from database", products.Count);

            var searchDocuments = new List<ProductSearchDocument>();
            int embeddingsGeneratedCount = 0;

            foreach (var product in products)
            {
                try
                {
                    // Combine name and description for embedding
                    var textForEmbedding = $"{product.Name}. {product.Description ?? string.Empty}";
                    
                    // Generate embeddings
                    var embeddings = await GenerateEmbeddingsAsync(textForEmbedding);

                    // Verify embeddings were generated
                    if (embeddings.Length == 0)
                    {
                        _logger.LogWarning("Empty embeddings generated for product {ProductId} ({ProductName})", product.Id, product.Name);
                        continue;
                    }

                    embeddingsGeneratedCount++;

                    // Create search document
                    var searchDocument = new ProductSearchDocument
                    {
                        Id = product.Id.ToString(),
                        Sku = product.Sku,
                        Name = product.Name,
                        Description = product.Description ?? string.Empty,
                        Category = product.Category ?? string.Empty,
                        Price = product.Price,
                        NameDescriptionVector = embeddings
                    };

                    searchDocuments.Add(searchDocument);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to generate embeddings for product {ProductId}", product.Id);
                }
            }

            _logger.LogInformation("✅ Successfully generated embeddings for {Count}/{Total} products", 
                embeddingsGeneratedCount, products.Count);

            // Upload documents to search index
            if (searchDocuments.Any())
            {
                _logger.LogInformation("Uploading {Count} documents to Azure AI Search index '{IndexName}'...", 
                    searchDocuments.Count, _indexName);
                
                var batch = IndexDocumentsBatch.Upload(searchDocuments);
                var result = await _searchClient.IndexDocumentsAsync(batch);
                
                var successCount = result.Value.Results.Count(r => r.Succeeded);
                var failedCount = result.Value.Results.Count(r => !r.Succeeded);
                
                if (failedCount > 0)
                {
                    _logger.LogWarning("⚠️ Upload completed with {FailedCount} failures", failedCount);
                    foreach (var failed in result.Value.Results.Where(r => !r.Succeeded))
                    {
                        _logger.LogWarning("Failed to index document {Key}: {ErrorMessage}", 
                            failed.Key, failed.ErrorMessage);
                    }
                }
                
                _logger.LogInformation("✅ Successfully uploaded {SuccessCount}/{TotalCount} products to search index", 
                    successCount, searchDocuments.Count);
                
                return successCount;
            }

            _logger.LogWarning("No documents to upload");
            return 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error indexing products");
            throw;
        }
    }

    public async Task<List<ProductSearchResult>> SearchProductsAsync(string query, int top = 10, string? categoryFilter = null)
    {
        try
        {
            _logger.LogInformation("Performing semantic search for query: {Query}", query);

            // Generate embeddings for the search query
            var queryEmbeddings = await GenerateEmbeddingsAsync(query);

            // Build search options with hybrid search (vector + keyword)
            var searchOptions = new SearchOptions
            {
                Size = top,
                Select = { "Id", "Sku", "Name", "Description", "Category", "Price" },
                IncludeTotalCount = true
            };

            // Add category filter if provided
            if (!string.IsNullOrWhiteSpace(categoryFilter))
            {
                // Validate and normalize category filter against whitelist to prevent OData filter injection
                var normalizedCategory = ProductCategories.GetNormalizedCategory(categoryFilter);
                if (normalizedCategory != null)
                {
                    // Escape the category value for safe use in OData filter
                    var escapedCategory = ProductCategories.EscapeForOData(normalizedCategory);
                    searchOptions.Filter = $"Category eq '{escapedCategory}'";
                }
                else
                {
                    _logger.LogWarning("Invalid category filter attempted: {CategoryFilter}", categoryFilter);
                    throw new ArgumentException($"Invalid category filter. Valid categories are: {string.Join(", ", ProductCategories.All)}", nameof(categoryFilter));
                }
            }

            // Configure vector search
            var vectorQuery = new VectorizedQuery(queryEmbeddings)
            {
                KNearestNeighborsCount = top,
                Fields = { "NameDescriptionVector" }
            };

            // Add vector query to search options
            searchOptions.VectorSearch = new VectorSearchOptions();
            searchOptions.VectorSearch.Queries.Add(vectorQuery);

            // Perform hybrid search (vector + keyword)
            var response = await _searchClient.SearchAsync<ProductSearchDocument>(
                query,
                searchOptions);

            var results = new List<ProductSearchResult>();

            await foreach (var result in response.Value.GetResultsAsync())
            {
                results.Add(new ProductSearchResult
                {
                    Id = int.Parse(result.Document.Id),
                    Sku = result.Document.Sku,
                    Name = result.Document.Name,
                    Description = result.Document.Description,
                    Category = result.Document.Category,
                    Price = result.Document.Price,
                    Score = result.Score ?? 0
                });
            }

            _logger.LogInformation("Search returned {Count} results", results.Count);
            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error performing semantic search");
            throw;
        }
    }

    public async Task<ReadOnlyMemory<float>> GenerateEmbeddingsAsync(string text)
    {
        try
        {
            _logger.LogInformation("Generating embeddings using deployment: {DeploymentName} at endpoint: {Endpoint}", 
                _embeddingDeploymentName, _configuration["AzureAI:Endpoint"]);
            var embeddingClient = _openAIClient.GetEmbeddingClient(_embeddingDeploymentName);
            
            // Add dimensions parameter for text-embedding-3-small (supports 1536 dimensions)
            var options = new OpenAI.Embeddings.EmbeddingGenerationOptions
            {
                Dimensions = 1536
            };
            
            var response = await embeddingClient.GenerateEmbeddingAsync(text, options);
            
            var embeddings = response.Value.ToFloats();
            _logger.LogInformation("Generated embedding with {Dimensions} dimensions", embeddings.Length);
            return embeddings;
        }
        catch (Azure.RequestFailedException ex)
        {
            _logger.LogError(ex, "Azure RequestFailedException generating embeddings. Status: {Status}, ErrorCode: {ErrorCode}, Message: {Message}, Response: {Response}", 
                ex.Status, ex.ErrorCode, ex.Message, ex.GetRawResponse()?.Content?.ToString());
            throw;
        }
        catch (System.ClientModel.ClientResultException ex)
        {
            var errorDetails = "No response details available";
            try
            {
                if (ex.GetRawResponse() != null)
                {
                    var response = ex.GetRawResponse();
                    errorDetails = $"Status: {response.Status}, ReasonPhrase: {response.ReasonPhrase}, Content: {response.Content}";
                }
            }
            catch { /* ignore error reading response */ }
            
            _logger.LogError(ex, "ClientResultException generating embeddings. Status: {Status}, Message: {Message}, Details: {Details}. Check: 1) Deployment '{Deployment}' exists, 2) Endpoint '{Endpoint}' is correct, 3) You have 'Cognitive Services OpenAI User' role", 
                ex.Status, ex.Message, errorDetails, _embeddingDeploymentName, _configuration["AzureAI:Endpoint"]);
            
            // Return user-friendly error without exposing internal details
            throw new InvalidOperationException("Unable to generate embeddings. Please contact support if this issue persists.", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating embeddings for text: {Text}. Deployment: {Deployment}, Endpoint: {Endpoint}", 
                text, _embeddingDeploymentName, _openAIClient.GetType().Name);
            
            // Return user-friendly error without exposing internal details
            throw new InvalidOperationException("Unable to generate embeddings. Please contact support if this issue persists.", ex);
        }
    }
}
