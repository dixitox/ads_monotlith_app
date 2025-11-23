using RetailDecomposed.Models;

namespace RetailDecomposed.Services;

/// <summary>
/// Service for semantic search functionality using Azure AI Search and Azure OpenAI embeddings.
/// </summary>
public interface ISemanticSearchService
{
    /// <summary>
    /// Creates or updates the Azure AI Search index with vector search configuration.
    /// </summary>
    /// <returns>True if index was created or updated successfully.</returns>
    Task<bool> CreateOrUpdateIndexAsync();

    /// <summary>
    /// Indexes all products from the database into Azure AI Search with embeddings.
    /// </summary>
    /// <returns>Number of products indexed.</returns>
    Task<int> IndexProductsAsync();

    /// <summary>
    /// Performs semantic search on products using natural language query.
    /// Combines vector search (embeddings) with keyword search for hybrid results.
    /// </summary>
    /// <param name="query">Natural language search query.</param>
    /// <param name="top">Maximum number of results to return (default: 10).</param>
    /// <param name="categoryFilter">Optional category filter.</param>
    /// <returns>List of matching products with relevance scores.</returns>
    Task<List<ProductSearchResult>> SearchProductsAsync(string query, int top = 10, string? categoryFilter = null);

    /// <summary>
    /// Generates embeddings for text using Azure OpenAI text-embedding-3-small model.
    /// </summary>
    /// <param name="text">Text to generate embeddings for.</param>
    /// <returns>Array of embeddings (1536 dimensions).</returns>
    Task<ReadOnlyMemory<float>> GenerateEmbeddingsAsync(string text);
}

/// <summary>
/// Represents a search result with product information and relevance score.
/// </summary>
public class ProductSearchResult
{
    public int Id { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public decimal Price { get; set; }
    
    /// <summary>
    /// Relevance score from Azure AI Search (higher is more relevant).
    /// </summary>
    public double Score { get; set; }
}
