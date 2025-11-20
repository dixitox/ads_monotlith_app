using RetailMonolith.Models;
using System.Net.Http.Json;

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

        public CartApiClient(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<Cart> GetCartAsync(string customerId, CancellationToken ct = default)
        {
            var response = await _httpClient.GetAsync($"/api/cart/{customerId}", ct);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<Cart>(cancellationToken: ct) 
                   ?? new Cart { CustomerId = customerId };
        }

        public async Task AddToCartAsync(string customerId, int productId, int quantity = 1, CancellationToken ct = default)
        {
            var response = await _httpClient.PostAsync(
                $"/api/cart/{customerId}/items?productId={productId}&quantity={quantity}", 
                null, 
                ct);
            response.EnsureSuccessStatusCode();
        }
    }
}
