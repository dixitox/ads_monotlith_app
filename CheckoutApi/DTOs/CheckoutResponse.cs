namespace CheckoutApi.DTOs
{
    public class CheckoutResponse
    {
        public int OrderId { get; set; }
        public string Status { get; set; } = default!;
        public decimal Total { get; set; }
        public DateTime CreatedUtc { get; set; }
    }
}
