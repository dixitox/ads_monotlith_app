using RetailMonolith.Models;
using System.Net.Http.Json;
using System.Diagnostics;

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
        private static readonly ActivitySource _activitySource = TelemetryActivitySources.Products;

        public ProductsApiClient(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<IList<Product>> GetProductsAsync(CancellationToken ct = default)
        {
            using var activity = _activitySource.StartActivity("GetProducts", ActivityKind.Client);
            activity?.SetTag("products.operation", "list");
            
            try
            {
                var response = await _httpClient.GetAsync("/api/products", ct);
                activity?.SetTag("http.status_code", (int)response.StatusCode);
                response.EnsureSuccessStatusCode();
                
                var products = await response.Content.ReadFromJsonAsync<List<Product>>(cancellationToken: ct) 
                    ?? new List<Product>();
                activity?.SetTag("products.count", products.Count);
                
                return products;
            }
            catch (Exception ex)
            {
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                activity?.RecordException(ex);
                throw;
            }
        }

        public async Task<Product?> GetProductByIdAsync(int productId, CancellationToken ct = default)
        {
            using var activity = _activitySource.StartActivity("GetProductById", ActivityKind.Client);
            activity?.SetTag("products.operation", "get_by_id");
            activity?.SetTag("products.id", productId);
            
            try
            {
                var response = await _httpClient.GetAsync($"/api/products/{productId}", ct);
                activity?.SetTag("http.status_code", (int)response.StatusCode);
                
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    activity?.SetTag("products.found", false);
                    return null;
                }
                
                response.EnsureSuccessStatusCode();
                var product = await response.Content.ReadFromJsonAsync<Product>(cancellationToken: ct);
                activity?.SetTag("products.found", product != null);
                
                return product;
            }
            catch (Exception ex)
            {
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                activity?.RecordException(ex);
                throw;
            }
        }
    }
}
