namespace CheckoutApi.DTOs
{
    public class CheckoutRequest
    {
        public string CustomerId { get; set; } = "guest";
        public string PaymentToken { get; set; } = default!;
        public int CartId { get; set; }
    }
}
