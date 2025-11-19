using RetailMonolith.Models;

namespace RetailMonolith.Services
{
    public interface ISearchService
    {
        Task InitializeIndexAsync(CancellationToken ct = default);
        Task IndexProductsAsync(CancellationToken ct = default);
        Task<IEnumerable<Product>> SearchProductsAsync(string query, int maxResults = 10, CancellationToken ct = default);
    }
}
