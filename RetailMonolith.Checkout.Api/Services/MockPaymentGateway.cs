namespace RetailMonolith.Checkout.Api.Services
{
    public class MockPaymentGateway : IPaymentGateway
    {
        public Task<PaymentResult> ChargeAsync(PaymentRequest req, CancellationToken ct = default)
        {
            // Trivial success for demo; always succeeds.
            return Task.FromResult(new PaymentResult(true, $"MOCK-{Guid.NewGuid():N}", null));
        }
    }
}
