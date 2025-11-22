
using Microsoft.EntityFrameworkCore;
using RetailMonolith.Models;

namespace RetailMonolith.Data
{
    public class AppDbContext:DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<Product> Products => Set<Product>();
        public DbSet<InventoryItem> Inventory => Set<InventoryItem>();
        public DbSet<Cart> Carts => Set<Cart>();
        public DbSet<CartLine> CartLines => Set<CartLine>();

        public DbSet<Order> Orders => Set<Order>();
        public DbSet<OrderLine> OrderLines => Set<OrderLine>();

      


        protected override void OnModelCreating(ModelBuilder b)
        {
            b.Entity<Product>().HasIndex(p => p.Sku).IsUnique();
            b.Entity<Product>().Property(p => p.Price).HasPrecision(18, 2);
            
            b.Entity<InventoryItem>().HasIndex(i => i.Sku).IsUnique();
            
            b.Entity<CartLine>().Property(c => c.UnitPrice).HasPrecision(18, 2);
            
            b.Entity<Order>().Property(o => o.Total).HasPrecision(18, 2);
            b.Entity<OrderLine>().Property(o => o.UnitPrice).HasPrecision(18, 2);
        }

        public static async Task SeedAsync(AppDbContext db)
        {
            if (!await db.Products.AnyAsync())
            {
                var random = new Random();
                var currency = "GBP";

                // Define realistic product names for each category
                var productNames = new Dictionary<string, List<string>>
                {
                    ["Beauty"] = new List<string> { "Radiant Glow Serum", "Luxury Hydrating Cream", "Velvet Matte Lipstick", "Rose Gold Eye Palette", "Deep Cleansing Face Mask", "Vitamin C Brightening Toner", "Anti-Aging Night Repair", "Natural Mineral Foundation", "Silk Finish Setting Spray", "Nourishing Hair Treatment" },
                    ["Apparel"] = new List<string> { "Classic Cotton T-Shirt", "Slim Fit Denim Jeans", "Wool Blend Blazer", "Cashmere Pullover Sweater", "Linen Summer Dress", "Athletic Performance Hoodie", "Tailored Chino Trousers", "Oversized Graphic Tee", "Quilted Puffer Jacket", "Stretch Yoga Leggings" },
                    ["Footwear"] = new List<string> { "Leather Oxford Shoes", "Running Sneakers Pro", "Ankle Chelsea Boots", "Canvas Slip-On Loafers", "Hiking Trail Boots", "Ballet Flat Pumps", "Sports Training Shoes", "Suede Desert Boots", "Memory Foam Slippers", "Waterproof Rain Boots" },
                    ["Home"] = new List<string> { "Memory Foam Pillow Set", "Egyptian Cotton Bedding", "Ceramic Dinnerware Collection", "Stainless Steel Cookware", "LED Smart Light Bulbs", "Bamboo Cutting Board Set", "Aromatherapy Diffuser", "Plush Microfiber Towels", "Glass Storage Containers", "Woven Throw Blanket" },
                    ["Accessories"] = new List<string> { "Leather Crossbody Bag", "Designer Sunglasses", "Stainless Steel Watch", "Silk Print Scarf", "Minimalist Wallet", "Statement Necklace Set", "Canvas Backpack", "Leather Belt Classic", "Pearl Stud Earrings", "Tech Organizer Pouch" },
                    ["Electronics"] = new List<string> { "Wireless Bluetooth Earbuds", "4K Smart TV Display", "Portable Power Bank", "Gaming Mechanical Keyboard", "HD Webcam Pro", "Noise-Cancelling Headphones", "Smart Fitness Tracker", "USB-C Fast Charger", "Wireless Charging Pad", "Bluetooth Speaker Portable" }
                };

                var allProducts = new List<Product>();
                int skuCounter = 1;

                foreach (var cat in productNames)
                {
                    foreach (var name in cat.Value)
                    {
                        var price = Math.Round((decimal)(random.NextDouble() * 100 + 5), 2);
                        var desc = GenerateDescription(name, cat.Key);
                        
                        allProducts.Add(new Product
                        {
                            Sku = $"SKU-{skuCounter:0000}",
                            Name = name,
                            Description = desc,
                            Price = price,
                            Currency = currency,
                            IsActive = true,
                            Category = cat.Key
                        });
                        skuCounter++;
                    }
                }

                await db.Products.AddRangeAsync(allProducts);
                await db.Inventory.AddRangeAsync(allProducts.Select(p => new InventoryItem { Sku = p.Sku, Quantity = random.Next(10,200) }));
                await db.SaveChangesAsync();
            }
        }

        private static string GenerateDescription(string name, string category)
        {
            var n = name.ToLower();
            if (n.Contains("serum") || n.Contains("cream") || n.Contains("toner")) return $"Luxurious skincare {n} formulated with premium ingredients for radiant, healthy-looking skin. Dermatologist tested and suitable for all skin types. Absorbs quickly without leaving residue.";
            if (n.Contains("lipstick") || n.Contains("foundation") || n.Contains("palette")) return $"High-pigment {n} delivers professional-quality color and flawless finish. Long-lasting formula with buildable coverage. Cruelty-free and paraben-free.";
            if (n.Contains("mask") || n.Contains("spray") || n.Contains("treatment")) return $"Professional-grade {n} designed to nourish and revitalize. Packed with vitamins and antioxidants for visible results. Easy to apply and suitable for daily use.";
            if (n.Contains("t-shirt") || n.Contains("tee") || n.Contains("hoodie")) return $"Comfortable {n} crafted from premium cotton blend fabric. Features modern fit and soft texture. Perfect for casual everyday wear. Machine washable.";
            if (n.Contains("jeans") || n.Contains("trousers") || n.Contains("leggings")) return $"Stylish {n} designed with stretch fabric for ultimate comfort and mobility. Flattering fit with reinforced stitching. Available in various sizes for perfect fit.";
            if (n.Contains("blazer") || n.Contains("sweater") || n.Contains("jacket") || n.Contains("dress")) return $"Elegant {n} that combines style with versatility. Premium fabric construction ensures durability and comfort. Ideal for both formal and casual occasions.";
            if (n.Contains("shoes") || n.Contains("boots") || n.Contains("sneakers")) return $"Premium {n} engineered for comfort and durability. Features cushioned insole and breathable materials. Non-slip outsole provides excellent traction on various surfaces.";
            if (n.Contains("loafers") || n.Contains("pumps") || n.Contains("slippers")) return $"Comfortable {n} perfect for all-day wear. Lightweight design with padded footbed. Classic style that complements any outfit.";
            if (n.Contains("pillow") || n.Contains("bedding") || n.Contains("towels") || n.Contains("blanket")) return $"Luxurious {n} made from high-quality materials for superior comfort. Soft, breathable, and easy to care for. Adds a touch of elegance to any bedroom or bathroom.";
            if (n.Contains("dinnerware") || n.Contains("cookware") || n.Contains("containers") || n.Contains("cutting board")) return $"Durable {n} designed for everyday use. Easy to clean and maintain. Functional design meets aesthetic appeal for modern kitchens.";
            if (n.Contains("light") || n.Contains("diffuser")) return $"Smart {n} enhances your living space with modern functionality. Energy-efficient and easy to operate. Creates the perfect ambiance for any room.";
            if (n.Contains("bag") || n.Contains("backpack") || n.Contains("wallet") || n.Contains("pouch")) return $"Stylish {n} crafted with attention to detail. Multiple compartments for organized storage. Durable construction with premium hardware and finishes.";
            if (n.Contains("sunglasses") || n.Contains("watch") || n.Contains("scarf") || n.Contains("belt")) return $"Fashionable {n} that adds sophistication to any outfit. Quality craftsmanship with timeless design. Perfect accessory for completing your look.";
            if (n.Contains("necklace") || n.Contains("earrings")) return $"Elegant {n} featuring refined design and quality materials. Lightweight and comfortable for all-day wear. Makes a perfect gift or personal treat.";
            if (n.Contains("earbuds") || n.Contains("headphones") || n.Contains("speaker")) return $"Premium audio {n} delivering crystal-clear sound quality. Advanced technology with long battery life. Comfortable design for extended listening sessions.";
            if (n.Contains("tv") || n.Contains("display") || n.Contains("webcam") || n.Contains("keyboard")) return $"High-performance {n} featuring cutting-edge technology. User-friendly interface with reliable functionality. Perfect for home, office, or entertainment use.";
            if (n.Contains("power bank") || n.Contains("charger") || n.Contains("charging pad")) return $"Reliable {n} ensures your devices stay powered throughout the day. Fast charging technology with built-in safety features. Compact and portable design.";
            if (n.Contains("tracker") || n.Contains("fitness")) return $"Advanced {n} helps you monitor and achieve your health goals. Accurate tracking with intuitive app integration. Water-resistant and comfortable for 24/7 wear.";
            return category switch
            {
                "Beauty" => $"Premium {n} formulated with quality ingredients for beautiful results. Suitable for daily use.",
                "Apparel" => $"Stylish {n} combining comfort and fashion. Perfect for any wardrobe.",
                "Footwear" => $"Comfortable {n} designed for all-day wear with quality construction.",
                "Home" => $"Essential {n} that enhances your living space with functionality and style.",
                "Accessories" => $"Fashionable {n} that complements any outfit with timeless appeal.",
                "Electronics" => $"Innovative {n} featuring modern technology and reliable performance.",
                _ => $"Quality {n} designed to meet your needs."
            };
        }
    }
}
