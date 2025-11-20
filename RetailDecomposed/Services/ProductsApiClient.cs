using RetailMonolith.Models;
using System.Net.Http.Json;

namespace RetailDecomposed.Services
{
    public interface IProductsApiClient
    {
        Task<IList<Product>> GetProductsAsync(CancellationToken ct = default);
        Task<Product?> GetProductByIdAsync(int productId, CancellationToken ct = default);
    }

    public class ProductsApiClient : IProductsApiClient
    {
        private readonly HttpClient _httpClient;

        public ProductsApiClient(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<IList<Product>> GetProductsAsync(CancellationToken ct = default)
        {
            var response = await _httpClient.GetAsync("/api/products", ct);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<List<Product>>(cancellationToken: ct) 
                ?? new List<Product>();
        }

        public async Task<Product?> GetProductByIdAsync(int productId, CancellationToken ct = default)
        {
            var response = await _httpClient.GetAsync($"/api/products/{productId}", ct);
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                return null;
            
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<Product>(cancellationToken: ct);
        }
    }
}
