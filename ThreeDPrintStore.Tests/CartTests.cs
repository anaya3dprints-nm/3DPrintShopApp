using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ThreeDPrintStore.Data;
using Xunit;

namespace ThreeDPrintStore.Tests;

public class CartTests
{
    [Fact]
    public async Task Cart_ignores_client_price_and_enforces_product_and_quantity_limits()
    {
        using var factory = new StoreFactory();
        using var client = factory.CreateStoreClient();
        var token = await HttpTestHelpers.AntiforgeryToken(client, "/Cart/Index");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/Cart/AddAjax", new FormUrlEncodedContent(
            new Dictionary<string, string> { ["productId"] = "2" }))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/Cart/AddAjax", HttpTestHelpers.Form(token, ("productId", "999")))).StatusCode);
        for (var i = 0; i < 5; i++)
        {
            var response = await client.PostAsync("/Cart/AddAjax", HttpTestHelpers.Form(token,
                ("productId", "2"), ("name", "Forged"), ("price", "0.01")));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/Cart/AddAjax", HttpTestHelpers.Form(token, ("productId", "2")))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/Cart/Update", HttpTestHelpers.Form(token, ("productId", "2"), ("quantity", "6")))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/Cart/Update", HttpTestHelpers.Form(token, ("productId", "2"), ("quantity", "-1")))).StatusCode);
        var html = await client.GetStringAsync("/Cart/Index");
        Assert.Contains("Roswell Alien Duck", html);
        Assert.DoesNotContain("Forged", html);
        Assert.Contains("$5.00", html);
        Assert.Contains("$25.00", html);
        Assert.Equal(HttpStatusCode.Redirect, (await client.PostAsync("/Cart/Update", HttpTestHelpers.Form(token, ("productId", "2"), ("quantity", "0")))).StatusCode);
        Assert.DoesNotContain("Roswell Alien Duck", await client.GetStringAsync("/Cart/Index"));
    }

    [Theory]
    [InlineData(9, false, 4500, 895)]
    [InlineData(9, true, 5000, 0)]
    [InlineData(10, false, 5000, 0)]
    public async Task Checkout_uses_trusted_prices_and_threshold_including_addon(int quantity, bool addon, long subtotal, long shipping)
    {
        using var factory = new StoreFactory();
        using var client = factory.CreateStoreClient();
        factory.InitializeDatabase();
        var token = await HttpTestHelpers.AntiforgeryToken(client, "/Cart/Index");
        await client.PostAsync("/Cart/AddAjax", HttpTestHelpers.Form(token, ("productId", "1"), ("price", "0.01")));
        await client.PostAsync("/Cart/Update", HttpTestHelpers.Form(token, ("productId", "1"), ("quantity", quantity.ToString())));
        var response = await client.PostAsync("/Cart/Checkout", HttpTestHelpers.Form(token,
            ("includeCacheDrop", addon.ToString()), ("sponsorName", "  Sponsor  ")));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("https://checkout.example/session", response.Headers.Location?.OriginalString);

        var stripe = Assert.Single(factory.CheckoutRequests);
        Assert.Equal(500, stripe.LineItems[0].PriceData.UnitAmount);
        Assert.Equal(quantity, stripe.LineItems[0].Quantity);
        Assert.Equal(addon ? 2 : 1, stripe.LineItems.Count);
        if (addon) Assert.Equal(500, stripe.LineItems[1].PriceData.UnitAmount);
        Assert.Equal(shipping, stripe.ShippingOptions[0].ShippingRateData.FixedAmount.Amount);
        Assert.StartsWith("https://shop.example/", stripe.SuccessUrl);
        using var scope = factory.Services.CreateScope();
        var order = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Orders.Include(o => o.Items).SingleAsync();
        Assert.Equal(subtotal, order.SubtotalCents);
        Assert.Equal(shipping, order.ShippingCents);
        Assert.Equal(subtotal + shipping, order.TotalCents);
        Assert.Equal(addon, order.PremiumCacheDrop);
        Assert.Equal(addon ? "Sponsor" : null, order.SponsorName);
        Assert.Equal(500, Assert.Single(order.Items).UnitPriceCents);
    }
}
