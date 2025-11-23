using RetailDecomposed.Models;
using System.Net.Http.Json;
using System.Diagnostics;

namespace RetailDecomposed.Services
{
    public interface ICartApiClient
    {
        Task<Cart> GetCartAsync(string customerId, CancellationToken ct = default);
        Task AddToCartAsync(string customerId, int productId, int quantity = 1, CancellationToken ct = default);
    }

    public class CartApiClient : ICartApiClient
    {
        private readonly HttpClient _httpClient;
        private static readonly ActivitySource _activitySource = TelemetryActivitySources.Cart;

        public CartApiClient(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<Cart> GetCartAsync(string customerId, CancellationToken ct = default)
        {
            using var activity = _activitySource.StartActivity("GetCart", ActivityKind.Client);
            activity?.SetTag("cart.operation", "get");
            activity?.SetTag("cart.customer_id", customerId);
            
            try
            {
                var response = await _httpClient.GetAsync($"/api/cart/{customerId}", ct);
                activity?.SetTag("http.status_code", (int)response.StatusCode);
                response.EnsureSuccessStatusCode();
                
                var cart = await response.Content.ReadFromJsonAsync<Cart>(cancellationToken: ct) 
                       ?? new Cart { CustomerId = customerId };
                activity?.SetTag("cart.items_count", cart.Lines?.Count ?? 0);
                
                return cart;
            }
            catch (Exception ex)
            {
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                activity?.RecordException(ex);
                throw;
            }
        }

        public async Task AddToCartAsync(string customerId, int productId, int quantity = 1, CancellationToken ct = default)
        {
            using var activity = _activitySource.StartActivity("AddToCart", ActivityKind.Client);
            activity?.SetTag("cart.operation", "add_item");
            activity?.SetTag("cart.customer_id", customerId);
            activity?.SetTag("cart.product_id", productId);
            activity?.SetTag("cart.quantity", quantity);
            
            try
            {
                var response = await _httpClient.PostAsync(
                    $"/api/cart/{customerId}/items?productId={productId}&quantity={quantity}", 
                    null, 
                    ct);
                activity?.SetTag("http.status_code", (int)response.StatusCode);
                response.EnsureSuccessStatusCode();
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
