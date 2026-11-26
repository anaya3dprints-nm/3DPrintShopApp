using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using ThreeDPrintStore.Data;
using Xunit;

namespace ThreeDPrintStore.Tests;

public class CheckoutMigrationTests
{
    [Fact]
    public async Task Existing_order_survives_checkout_migration_and_model_has_no_pending_changes()
    {
        using var factory = new StoreFactory();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.GetService<IMigrator>().MigrateAsync("20261126004731_AddOrders");
        await db.Database.ExecuteSqlRawAsync("""
            INSERT INTO Orders (CreatedAtUtc, Status, SubtotalCents, ShippingCents, TotalCents, Currency, CheckoutToken, PremiumCacheDrop)
            VALUES ('2026-01-01 00:00:00', 0, 500, 895, 1395, 'usd', 'legacy-token', 0)
            """);

        await db.Database.MigrateAsync();
        Assert.False(db.Database.HasPendingModelChanges());
        var order = await db.Orders.SingleAsync();
        Assert.Equal("legacy-token", order.CheckoutToken);
        Assert.Equal(1395, order.TotalCents);
        Assert.Null(order.ActiveCartKey);
        Assert.Empty(await db.CheckoutSubmissions.ToListAsync());
    }
}
