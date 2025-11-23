using RetailDecomposed.Models;
using System.Net.Http.Json;
using System.Diagnostics;

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
        private static readonly ActivitySource _activitySource = TelemetryActivitySources.Orders;

        public OrdersApiClient(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<IList<Order>> GetOrdersAsync(CancellationToken ct = default)
        {
            using var activity = _activitySource.StartActivity("GetOrders", ActivityKind.Client);
            activity?.SetTag("orders.operation", "list");
            
            try
            {
                var response = await _httpClient.GetAsync("/api/orders", ct);
                activity?.SetTag("http.status_code", (int)response.StatusCode);
                response.EnsureSuccessStatusCode();
                
                var orders = await response.Content.ReadFromJsonAsync<List<Order>>(cancellationToken: ct) 
                    ?? new List<Order>();
                activity?.SetTag("orders.count", orders.Count);
                
                return orders;
            }
            catch (Exception ex)
            {
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                activity?.RecordException(ex);
                throw;
            }
        }

        public async Task<Order?> GetOrderByIdAsync(int orderId, CancellationToken ct = default)
        {
            using var activity = _activitySource.StartActivity("GetOrderById", ActivityKind.Client);
            activity?.SetTag("orders.operation", "get_by_id");
            activity?.SetTag("orders.id", orderId);
            
            try
            {
                var response = await _httpClient.GetAsync($"/api/orders/{orderId}", ct);
                activity?.SetTag("http.status_code", (int)response.StatusCode);
                
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    activity?.SetTag("orders.found", false);
                    return null;
                }
                
                response.EnsureSuccessStatusCode();
                var order = await response.Content.ReadFromJsonAsync<Order>(cancellationToken: ct);
                activity?.SetTag("orders.found", order != null);
                
                return order;
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
