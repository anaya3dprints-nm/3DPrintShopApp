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
                    Description = "This hauntingly charming duck pays tribute to one of New Mexico’s most iconic legends.",
                    Price = 5.00m,
                    StockQuantity = 12,
                    ImageUrl = "https://unsplash.com" // Placeholder clean 3D print render asset
                },
                new Product
                {
                    Name = "Red Chile Duck",
                    Description = "Representing the deep, rich spice of New Mexico’s signature red chile, this duck radiates warmth and tradition.",
                    Price = 5.00m,
                    StockQuantity = 13,
                    ImageUrl = "https://unsplash.com"
                },
                new Product
                {
                    Name = "Green Chile Duck",
                    Description = "Bold, flavorful, and proudly New Mexican—this duck celebrates the beloved green chile that defines the state’s cuisine",
                    Price = 5.00m,
                    StockQuantity = 8,
                    ImageUrl = "https://unsplash.com"
                },
                new Product
                {
                    Name = "Roswell Alien Duck",
                    Description = "A fun twist on New Mexico’s UFO capital, this extraterrestrial-inspired duck brings cosmic curiosity to life.",
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
