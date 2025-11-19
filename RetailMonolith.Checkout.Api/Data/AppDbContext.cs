using Microsoft.EntityFrameworkCore;
using RetailMonolith.Checkout.Api.Models;

namespace RetailMonolith.Checkout.Api.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<InventoryItem> Inventory => Set<InventoryItem>();
        public DbSet<Cart> Carts => Set<Cart>();
        public DbSet<CartLine> CartLines => Set<CartLine>();
        public DbSet<Order> Orders => Set<Order>();
        public DbSet<OrderLine> OrderLines => Set<OrderLine>();

        protected override void OnModelCreating(ModelBuilder b)
        {
            b.Entity<InventoryItem>().HasIndex(i => i.Sku).IsUnique();
        }
    }
}
