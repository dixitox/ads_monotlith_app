using Microsoft.EntityFrameworkCore;
using CheckoutApi.Models;

namespace CheckoutApi.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<Cart> Carts => Set<Cart>();
        public DbSet<CartLine> CartLines => Set<CartLine>();
        public DbSet<Order> Orders => Set<Order>();
        public DbSet<OrderLine> OrderLines => Set<OrderLine>();
        public DbSet<InventoryItem> Inventory => Set<InventoryItem>();

        protected override void OnModelCreating(ModelBuilder b)
        {
            b.Entity<InventoryItem>().HasIndex(i => i.Sku).IsUnique();
        }
    }
}
