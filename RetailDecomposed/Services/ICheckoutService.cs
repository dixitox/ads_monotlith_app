using RetailDecomposed.Models;

namespace RetailDecomposed.Services
{
    public interface ICheckoutService
    {
        Task<Order> CheckoutAsync(string customerId, string paymentToken, CancellationToken ct = default);
    }
}
