using RetailDecomposed.Models;
using System.Net.Http.Json;

namespace RetailDecomposed.Services
{
    public interface IOrdersApiClient
    {
        Task<IList<Order>> GetOrdersAsync(CancellationToken ct = default);
        Task<Order?> GetOrderByIdAsync(int orderId, CancellationToken ct = default);
    }

    public class OrdersApiClient : IOrdersApiClient
    {
        private readonly HttpClient _httpClient;

        public OrdersApiClient(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<IList<Order>> GetOrdersAsync(CancellationToken ct = default)
        {
            var response = await _httpClient.GetAsync("/api/orders", ct);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<List<Order>>(cancellationToken: ct) 
                ?? new List<Order>();
        }

        public async Task<Order?> GetOrderByIdAsync(int orderId, CancellationToken ct = default)
        {
            var response = await _httpClient.GetAsync($"/api/orders/{orderId}", ct);
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                return null;
            
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<Order>(cancellationToken: ct);
        }
    }
}
