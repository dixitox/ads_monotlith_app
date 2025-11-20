using RetailMonolith.Models;
using System.Net.Http.Json;

namespace RetailDecomposed.Services
{
    public interface ICheckoutApiClient
    {
        Task<Order> CheckoutAsync(string customerId, string paymentToken, CancellationToken ct = default);
    }

    public class CheckoutApiClient : ICheckoutApiClient
    {
        private readonly HttpClient _http;

        public CheckoutApiClient(HttpClient http)
        {
            _http = http;
        }

        public async Task<Order> CheckoutAsync(string customerId, string paymentToken, CancellationToken ct = default)
        {
            var request = new { CustomerId = customerId, PaymentToken = paymentToken };
            var response = await _http.PostAsJsonAsync("/api/checkout", request, ct);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<Order>(cancellationToken: ct)
                ?? throw new InvalidOperationException("Failed to deserialize order from checkout response");
        }
    }
}
