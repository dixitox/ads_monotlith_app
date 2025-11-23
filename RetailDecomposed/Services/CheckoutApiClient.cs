using RetailDecomposed.Models;
using System.Net.Http.Json;
using System.Diagnostics;

namespace RetailDecomposed.Services
{
    public interface ICheckoutApiClient
    {
        Task<Order> CheckoutAsync(string customerId, string paymentToken, CancellationToken ct = default);
    }

    public class CheckoutApiClient : ICheckoutApiClient
    {
        private readonly HttpClient _http;
        private static readonly ActivitySource _activitySource = TelemetryActivitySources.Checkout;

        public CheckoutApiClient(HttpClient http)
        {
            _http = http;
        }

        public async Task<Order> CheckoutAsync(string customerId, string paymentToken, CancellationToken ct = default)
        {
            using var activity = _activitySource.StartActivity("Checkout", ActivityKind.Client);
            activity?.SetTag("checkout.operation", "process");
            activity?.SetTag("checkout.customer_id", customerId);
            
            try
            {
                var request = new { CustomerId = customerId, PaymentToken = paymentToken };
                var response = await _http.PostAsJsonAsync("/api/checkout", request, ct);
                activity?.SetTag("http.status_code", (int)response.StatusCode);
                response.EnsureSuccessStatusCode();
                
                var order = await response.Content.ReadFromJsonAsync<Order>(cancellationToken: ct)
                    ?? throw new InvalidOperationException("Failed to deserialize order from checkout response");
                
                activity?.SetTag("checkout.order_id", order.Id);
                activity?.SetTag("checkout.order_total", order.Total);
                
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
