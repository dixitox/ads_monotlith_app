using CheckoutApi.Models;

namespace CheckoutApi.Services
{
    public interface ICheckoutService
    {
        Task<Order> CheckoutAsync(string customerId, string paymentToken, CancellationToken ct = default);
    }
}
