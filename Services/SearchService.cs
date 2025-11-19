using Azure;
using Azure.AI.OpenAI;
using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;
using Azure.Search.Documents.Models;
using Microsoft.EntityFrameworkCore;
using OpenAI.Embeddings;
using RetailMonolith.Data;
using RetailMonolith.Models;

namespace RetailMonolith.Services
{
    public class SearchService : ISearchService
    {
        private readonly AppDbContext _db;
        private readonly ILogger<SearchService> _logger;
        private readonly AzureOpenAIConfiguration _openAIConfig;
        private readonly AzureSearchConfiguration _searchConfig;
        private readonly SearchIndexClient _indexClient;
        private readonly SearchClient _searchClient;
        private readonly AzureOpenAIClient _openAIClient;
        private readonly EmbeddingClient _embeddingClient;

        public SearchService(
            AppDbContext db,
            ILogger<SearchService> logger,
            AzureOpenAIConfiguration openAIConfig,
            AzureSearchConfiguration searchConfig)
        {
            _db = db;
            _logger = logger;
            _openAIConfig = openAIConfig;
            _searchConfig = searchConfig;

            // Initialize Azure Search clients
            var searchCredential = new AzureKeyCredential(_searchConfig.ApiKey);
            _indexClient = new SearchIndexClient(new Uri(_searchConfig.Endpoint), searchCredential);
            _searchClient = _indexClient.GetSearchClient(_searchConfig.IndexName);

            // Initialize Azure OpenAI clients
            var openAICredential = new AzureKeyCredential(_openAIConfig.ApiKey);
            _openAIClient = new AzureOpenAIClient(new Uri(_openAIConfig.Endpoint), openAICredential);
            _embeddingClient = _openAIClient.GetEmbeddingClient(_openAIConfig.EmbeddingDeployment);
        }

        public async Task InitializeIndexAsync(CancellationToken ct = default)
        {
            try
            {
                _logger.LogInformation("Initializing search index: {IndexName}", _searchConfig.IndexName);

                // Define the vector search configuration
                var vectorSearch = new VectorSearch();
                vectorSearch.Profiles.Add(new VectorSearchProfile("vector-profile", "hnsw-config"));
                vectorSearch.Algorithms.Add(new HnswAlgorithmConfiguration("hnsw-config"));

                // Define the fields for the index
                var fieldBuilder = new FieldBuilder();
                var searchFields = fieldBuilder.Build(typeof(ProductSearchDocument));

                // Create the search index
                var definition = new SearchIndex(_searchConfig.IndexName, searchFields)
                {
                    VectorSearch = vectorSearch
                };

                await _indexClient.CreateOrUpdateIndexAsync(definition, cancellationToken: ct);
                _logger.LogInformation("Search index initialized successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing search index");
                throw;
            }
        }

        public async Task IndexProductsAsync(CancellationToken ct = default)
        {
            try
            {
                _logger.LogInformation("Starting product indexing");

                // Fetch all active products
                var products = await _db.Products
                    .Where(p => p.IsActive)
                    .ToListAsync(ct);

                _logger.LogInformation("Found {Count} active products to index", products.Count);

                // Convert products to search documents with embeddings
                var searchDocuments = new List<ProductSearchDocument>();
                
                foreach (var product in products)
                {
                    // Generate text for embedding
                    var textToEmbed = $"{product.Name} {product.Description ?? ""} {product.Category ?? ""}";
                    
                    // Generate embedding
                    var embedding = await GenerateEmbeddingAsync(textToEmbed, ct);

                    var searchDoc = new ProductSearchDocument
                    {
                        Id = product.Id.ToString(),
                        Sku = product.Sku,
                        Name = product.Name,
                        Description = product.Description,
                        Category = product.Category,
                        Price = product.Price,
                        IsActive = product.IsActive,
                        Embedding = embedding
                    };

                    searchDocuments.Add(searchDoc);
                }

                // Upload documents to Azure AI Search
                if (searchDocuments.Any())
                {
                    var batch = IndexDocumentsBatch.Upload(searchDocuments);
                    await _searchClient.IndexDocumentsAsync(batch, cancellationToken: ct);
                    _logger.LogInformation("Successfully indexed {Count} products", searchDocuments.Count);
                }
                else
                {
                    _logger.LogWarning("No products to index");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error indexing products");
                throw;
            }
        }

        public async Task<IEnumerable<Product>> SearchProductsAsync(string query, int maxResults = 10, CancellationToken ct = default)
        {
            try
            {
                _logger.LogInformation("Searching for products with query: {Query}", query);

                // Generate embedding for the search query
                var queryEmbedding = await GenerateEmbeddingAsync(query, ct);

                // Configure the search options with hybrid search (vector + keyword)
                var searchOptions = new SearchOptions
                {
                    Size = maxResults,
                    Select = { "Id", "Sku", "Name", "Description", "Category", "Price", "IsActive" },
                    Filter = "IsActive eq true"
                };

                // Add vector search
                var vectorQuery = new VectorizedQuery(queryEmbedding)
                {
                    KNearestNeighborsCount = maxResults,
                    Fields = { "Embedding" }
                };
                searchOptions.VectorSearch = new VectorSearchOptions();
                searchOptions.VectorSearch.Queries.Add(vectorQuery);

                // Execute the search
                var response = await _searchClient.SearchAsync<ProductSearchDocument>(query, searchOptions, ct);

                // Extract product IDs from search results
                var productIds = new List<int>();
                await foreach (var result in response.Value.GetResultsAsync())
                {
                    if (int.TryParse(result.Document.Id, out int productId))
                    {
                        productIds.Add(productId);
                    }
                }

                // Retrieve full product details from database maintaining search order
                if (productIds.Any())
                {
                    var products = await _db.Products
                        .Where(p => productIds.Contains(p.Id))
                        .ToListAsync(ct);

                    // Maintain the order from search results
                    var orderedProducts = productIds
                        .Select(id => products.FirstOrDefault(p => p.Id == id))
                        .Where(p => p != null)
                        .ToList();

                    _logger.LogInformation("Found {Count} products matching query", orderedProducts.Count);
                    return orderedProducts!;
                }

                return Enumerable.Empty<Product>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching products");
                throw;
            }
        }

        private async Task<ReadOnlyMemory<float>> GenerateEmbeddingAsync(string text, CancellationToken ct = default)
        {
            try
            {
                var response = await _embeddingClient.GenerateEmbeddingAsync(text, cancellationToken: ct);
                return response.Value.ToFloats();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating embedding for text: {Text}", text);
                throw;
            }
        }
    }
}
