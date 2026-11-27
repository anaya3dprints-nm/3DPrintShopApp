using Microsoft.EntityFrameworkCore;
using ThreeDPrintStore.Models; // Add this line

namespace ThreeDPrintStore.Data
{
	public class ApplicationDbContext : DbContext
	{
		public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
		{
		}
		
		public DbSet<PrintRequest> PrintRequests => Set<PrintRequest>();
		public DbSet<PrintQuote> PrintQuotes => Set<PrintQuote>();
		public DbSet<Order> Orders => Set<Order>();
		public DbSet<OrderItem> OrderItems => Set<OrderItem>();
		public DbSet<OrderNotification> OrderNotifications => Set<OrderNotification>();
		public DbSet<CheckoutSubmission> CheckoutSubmissions => Set<CheckoutSubmission>();
		
		protected override void OnModelCreating(ModelBuilder modelBuilder)
		{
			base.OnModelCreating(modelBuilder);
			modelBuilder.Entity<Order>().HasIndex(o => o.StripeSessionId).IsUnique();
			modelBuilder.Entity<Order>().HasIndex(o => o.ActiveCartKey).IsUnique();
			modelBuilder.Entity<CheckoutSubmission>().HasKey(s => s.Token);
			modelBuilder.Entity<OrderNotification>().HasIndex(n => new { n.OrderId, n.Kind }).IsUnique();
			
			modelBuilder.Entity<PrintRequest>()
				.HasOne(r => r.Quote)
				.WithOne(q => q.PrintRequest)
				.HasForeignKey<PrintQuote>(q => q.PrintRequestId)
				.OnDelete(DeleteBehavior.Cascade);
		}
	}
}
