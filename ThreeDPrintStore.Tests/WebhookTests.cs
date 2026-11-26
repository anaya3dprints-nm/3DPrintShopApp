using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Stripe;
using ThreeDPrintStore.Data;
using ThreeDPrintStore.Models;
using Xunit;

namespace ThreeDPrintStore.Tests;

public class WebhookTests
{
    [Fact]
    public async Task Paid_event_recovers_session_when_webhook_arrives_before_session_is_saved()
    {
        using var factory = new StoreFactory();
        using var client = factory.CreateStoreClient();
        factory.InitializeDatabase();
        int id;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var order = new Order { CheckoutToken = "test-order-token", TotalCents = 1395, Currency = "usd" };
            db.Orders.Add(order);
            await db.SaveChangesAsync();
            id = order.Id;
        }
        Assert.Equal(HttpStatusCode.OK, (await Send(client, Payload(id, "cs_test_1", 1395, "paid"))).StatusCode);
        using var finalScope = factory.Services.CreateScope();
        var dbFinal = finalScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var paid = await dbFinal.Orders.SingleAsync();
        Assert.Equal(OrderStatus.Paid, paid.Status);
        Assert.Equal("cs_test_1", paid.StripeSessionId);
        Assert.Single(await dbFinal.OrderNotifications.ToListAsync());
    }

    [Fact]
    public async Task Signed_paid_order_event_is_idempotent_and_never_regresses_status()
    {
        using var factory = new StoreFactory();
        using var client = factory.CreateStoreClient();
        factory.InitializeDatabase();
        int id;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var order = new Order { StripeSessionId = "cs_test_1", CheckoutToken = "test-order-token", TotalCents = 1395, Currency = "usd" };
            db.Orders.Add(order);
            await db.SaveChangesAsync();
            id = order.Id;
        }

        var good = Payload(id, "cs_test_1", 1395, "paid");
        Assert.Equal(HttpStatusCode.BadRequest, (await Send(client, good, false)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Send(client, Payload(id, "cs_wrong", 1395, "paid"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Send(client, Payload(id, "cs_test_1", 1, "paid"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Send(client, good.Replace("test-order-token", "wrong-token"))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Send(client, Payload(id, "cs_test_1", 1395, "unpaid"))).StatusCode);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.Equal(OrderStatus.PendingPayment, (await db.Orders.SingleAsync()).Status);
            Assert.Empty(await db.OrderNotifications.ToListAsync());
        }

        Assert.Equal(HttpStatusCode.OK, (await Send(client, good)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Send(client, good)).StatusCode);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var order = await db.Orders.SingleAsync();
            Assert.Equal(OrderStatus.Paid, order.Status);
            Assert.Equal("pi_test_1", order.StripePaymentIntentId);
            Assert.Equal("buyer@example.com", order.CustomerEmail);
            Assert.Single(await db.OrderNotifications.ToListAsync());
            order.Status = OrderStatus.Shipped;
            await db.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.OK, (await Send(client, good)).StatusCode);
        using var finalScope = factory.Services.CreateScope();
        var finalDb = finalScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(OrderStatus.Shipped, (await finalDb.Orders.SingleAsync()).Status);
        Assert.Single(await finalDb.OrderNotifications.ToListAsync());
    }

    [Fact]
    public async Task Paid_quote_webhook_replays_do_not_reverse_progress_or_replace_payment()
    {
        using var factory = new StoreFactory();
        using var client = factory.CreateStoreClient();
        factory.InitializeDatabase();
        int requestId, quoteId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var request = new PrintRequest
            {
                CustomerName = "Buyer", CustomerEmail = "buyer@example.com", ProjectDescription = "Test",
                Status = QuoteStatus.QuoteSent, Quote = new PrintQuote { FinalTotalPrice = 42.50m }
            };
            db.PrintRequests.Add(request);
            await db.SaveChangesAsync();
            requestId = request.Id;
            quoteId = request.Quote.Id;
        }
        var good = QuotePayload(requestId, quoteId, 4250, "pi_quote_1");
        Assert.Equal(HttpStatusCode.BadRequest, (await Send(client, QuotePayload(requestId, quoteId, 1, "pi_quote_1"))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Send(client, good)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Send(client, good)).StatusCode);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var request = await db.PrintRequests.Include(r => r.Quote).SingleAsync();
            Assert.Equal(QuoteStatus.Paid, request.Status);
            Assert.Equal("pi_quote_1", request.Quote!.StripePaymentIntentId);
            request.Status = QuoteStatus.InProduction;
            await db.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.OK, (await Send(client, QuotePayload(requestId, quoteId, 4250, "pi_other"))).StatusCode);
        using var finalScope = factory.Services.CreateScope();
        var finalRequest = await finalScope.ServiceProvider.GetRequiredService<ApplicationDbContext>()
            .PrintRequests.Include(r => r.Quote).SingleAsync();
        Assert.Equal(QuoteStatus.InProduction, finalRequest.Status);
        Assert.Equal("pi_quote_1", finalRequest.Quote!.StripePaymentIntentId);
    }

    private static string QuotePayload(int requestId, int quoteId, long total, string paymentIntent) =>
        JsonSerializer.Serialize(new
        {
            id = "evt_quote", @object = "event", api_version = StripeConfiguration.ApiVersion,
            type = "checkout.session.completed",
            data = new { @object = new
            {
                id = "cs_quote", @object = "checkout.session", mode = "payment", payment_status = "paid",
                currency = "usd", amount_total = total, payment_intent = paymentIntent,
                metadata = new Dictionary<string, string>
                {
                    ["request_id"] = requestId.ToString(), ["quote_id"] = quoteId.ToString()
                }
            } }
        });

    private static string Payload(int id, string sessionId, long total, string paymentStatus) =>
        JsonSerializer.Serialize(new
        {
            id = "evt_test_1",
            @object = "event",
            api_version = StripeConfiguration.ApiVersion,
            type = "checkout.session.completed",
            data = new { @object = new
            {
                id = sessionId, @object = "checkout.session", mode = "payment", payment_status = paymentStatus,
                currency = "usd", amount_total = total, payment_intent = "pi_test_1",
                metadata = new Dictionary<string, string> { ["order_id"] = id.ToString(), ["order_token"] = "test-order-token" },
                customer_details = new { email = "buyer@example.com" },
                collected_information = new { shipping_details = new
                {
                    name = "Buyer", address = new { line1 = "1 Test St", city = "Albuquerque", state = "NM", postal_code = "87101", country = "US" }
                } }
            } }
        });

    private static Task<HttpResponseMessage> Send(HttpClient client, string payload, bool valid = true)
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var hash = HMACSHA256.HashData(Encoding.UTF8.GetBytes("whsec_test_only"),
            Encoding.UTF8.GetBytes($"{timestamp}.{payload}"));
        var signature = valid ? Convert.ToHexString(hash).ToLowerInvariant() : new string('0', 64);
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/webhook/stripe")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        request.Headers.Add("Stripe-Signature", $"t={timestamp},v1={signature}");
        return client.SendAsync(request);
    }
}
