using Microsoft.EntityFrameworkCore;

namespace Custom3DPrints.Data
{
	public class ApplicationDbContext : DbContext
	{
		public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
		{
		}
		
		public DbSet<PrintRequest> PrintRequests => Set<PrintRequest>();
		public DbSet<PrintQuote> PrintQuotes => Set<PriceQuote>();
		
		proteted override void OnModelCreating(ModelBuilder modelBuilder)
		{
			base.OnModelCreating(modelBuilder);
			
			modelBuilder.Entity<PrintRequests>()
				.HasOne(r => r.Quote)
				.WithOne(q => q.PrintRequest)
				.HasForeignKey<PrintQuote>(q => q.PrintRequestId)
				.OnDelete(DeleteBehavior.Cascade);
		}
	}
}
