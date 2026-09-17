//This import allows project to use Entity Framework Core
This import allows model properties to use validation attributes like [Required], [EmailAddress], etc.
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace ThreeDPrintStore.Models //defines namespace where model classes live
{
    public class StoreDbContext : DbContext //creates a class called StoreDbContext which inherits from DbContext. Represents my SQLite db connection and schema
    {

        /**
            This is the constructor for your database context.
                DbContextOptions<StoreDbContext> contains the database configuration settings (like connection string).
                : base(options) passes those settings to the base class, completing EF’s setup.
                ASP.NET Core injects these options via dependency injection.
        **/
        public StoreDbContext(DbContextOptions<StoreDbContext> options) : base(options) { }

        public DbSet<Product> Products { get; set; } = null!; //defines a table named Products in db
        public DbSet<QuoteRequest> QuoteRequests { get; set; } = null!; //defines a table for quote requests users submit
        public DbSet<Order> Orders { get; set; } = null!; //defines a db table for orders during checkout

    }

    // 1. Regular Catalog Product Model
    public class Product
    {
        public int Id { get; set; }
        
        [Required]
        public string Name { get; set; } = string.Empty;
        
        [Required]
        public string Description { get; set; } = string.Empty;
        
        [Required]
        public decimal Price { get; set; }
        
        [Required]
        public int StockQuantity { get; set; }
        public string ImageUrl { get; set; } = "/images/placeholder.png";
    }

    // 2. Custom Quote Request Model
    public class QuoteRequest
    {
        public int Id { get; set; }
        
        [Required]
        public string CustomerEmail { get; set; } = string.Empty;
        
        [Required]
        public string Description { get; set; } = string.Empty;
        public string? ReferenceImagePath { get; set; }
        
        [Required]
        public string Size { get; set; } = string.Empty; // e.g., "100mm x 50mm"
        
        [Required]
        public string Color { get; set; } = string.Empty;
        
        [Required]
        public string FilamentType { get; set; } = "PLA";
        
        [Required]
        public string ShippingCity { get; set; } = string.Empty;
        
        [Required]
        public string PostalCode { get; set; } = string.Empty;

        // --- Hidden fields calculated later by you ---
        public decimal CalculatedPrice { get; set; }
        public decimal ShippingFee { get; set; }
        public string Status { get; set; } = "Pending"; // Pending, Calculated, Sent
    }
}
