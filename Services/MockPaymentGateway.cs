
namespace RetailMonolith.Services
{
    public class MockPaymentGateway : IPaymentGateway
    {
        public Task<PaymentResult> ChargeAsync(PaymentRequest req, CancellationToken ct = default)
        {
            // Mock payment gateway - always returns success for development/testing
            // In production, replace with actual payment provider integration
            return Task.FromResult(new PaymentResult(true, $"MOCK-{Guid.NewGuid():N}", null));
        }
    }
}
