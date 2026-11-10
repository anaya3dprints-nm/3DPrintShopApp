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
		
		protected override void OnModelCreating(ModelBuilder modelBuilder)
		{
			base.OnModelCreating(modelBuilder);
			
			modelBuilder.Entity<PrintRequest>()
				.HasOne(r => r.Quote)
				.WithOne(q => q.PrintRequest)
				.HasForeignKey<PrintQuote>(q => q.PrintRequestId)
				.OnDelete(DeleteBehavior.Cascade);
		}
	}
}
