using ThreeDPrintStore.Models;

namespace ThreeDPrintStore.Data
{
    public static class DbInitializer
    {
        public static void Seed(StoreDbContext context)
        {
            context.Database.EnsureCreated();

            // If products already exist, don't re-seed
            if (context.Products.Any()) return;

            var sampleProducts = new List<Product>
            {
                new Product
                {
                    Name = "La Llorona Duck",
                    Description = "Flexy articulated desktop dragon printed in a beautiful multi-color silk finish. Great for fidgeting.",
                    Price = 5.00m,
                    StockQuantity = 12,
                    ImageUrl = "https://unsplash.com" // Placeholder clean 3D print render asset
                },
                new Product
                {
                    Name = "Red Chile Duck",
                    Description = "Modern low-poly design pot perfect for small plants or desk succulents. Features integrated drainage holes.",
                    Price = 5.00m,
                    StockQuantity = 13,
                    ImageUrl = "https://unsplash.com"
                },
                new Product
                {
                    Name = "Green Chile Duck",
                    Description = "Heavy-duty mechanical under-desk clamp designed to keep your audio headset secure and accessible.",
                    Price = 5.00m,
                    StockQuantity = 8,
                    ImageUrl = "https://unsplash.com"
                },
                new Product
                {
                    Name = "Roswell Alien Duck",
                    Description = "",
                    Price = 5.00m,
                    StockQuantity = 8,
                    ImageUrl = "https://unsplash.com"
                }
            };

            context.Products.AddRange(sampleProducts);
            context.SaveChanges();
            
        }
    }
}
