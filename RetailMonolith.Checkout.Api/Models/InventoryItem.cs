namespace RetailMonolith.Checkout.Api.Models
{
    public class InventoryItem
    {
        public int Id { get; set; }
        public string Sku { get; set; } = default!;
        public int Quantity { get; set; }
    }
}
