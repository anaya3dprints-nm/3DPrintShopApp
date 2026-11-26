using System.Net;
using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ThreeDPrintStore.Data;
using ThreeDPrintStore.Models;
using Xunit;

namespace ThreeDPrintStore.Tests;

[CollectionDefinition("Stripe isolation", DisableParallelization = true)]
public class StripeIsolationCollection { }

[Collection("Stripe isolation")]
public class QuotePaymentTests
{
    [Fact]
    public async Task Admin_can_create_and_view_a_quote_but_cannot_send_or_pay_it()
    {
        using var factory = new StoreFactory();
        using var client = factory.CreateStoreClient();
        factory.InitializeDatabase();
        int requestId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var request = new PrintRequest
            {
                CustomerName = "Buyer",
                CustomerEmail = "buyer@example.com",
                ProjectDescription = "Custom model",
                RequestedColorsCount = 2,
                Status = QuoteStatus.PendingReview
            };
            db.PrintRequests.Add(request);
            await db.SaveChangesAsync();
            requestId = request.Id;
        }

        var loginToken = await HttpTestHelpers.AntiforgeryToken(client, "/Account/Login");
        var login = await client.PostAsync("/Account/Login", HttpTestHelpers.Form(loginToken,
            ("Username", StoreFactory.Username), ("Password", StoreFactory.Password)));
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);

        var detailsUrl = $"/Quote/Details/{requestId}";
        var token = await HttpTestHelpers.AntiforgeryToken(client, detailsUrl);
        var generate = await client.PostAsync("/Quote/GenerateQuote", HttpTestHelpers.Form(token,
            ("PrintRequestId", requestId.ToString(CultureInfo.InvariantCulture)),
            ("ColorCount", "2"), ("FilamentGrams", "100"), ("EstimatedPrintHours", "2"),
            ("MaterialCostPerGram", "0.05"), ("HourlyMachineRate", "5"),
            ("MultiColorFeePerColor", "2.5"), ("BaseHandlingFee", "3")));
        Assert.Equal(HttpStatusCode.Redirect, generate.StatusCode);
        var details = await client.GetAsync(detailsUrl);
        Assert.Equal(HttpStatusCode.OK, details.StatusCode);
        var detailsHtml = await details.Content.ReadAsStringAsync();
        Assert.Contains("Generated Quote Breakdown", detailsHtml);
        Assert.Contains("Quote payment and payment-link emails are temporarily unavailable", detailsHtml);
        Assert.DoesNotContain("Send Quote to Customer", detailsHtml);

        int quoteId;
        using (var scope = factory.Services.CreateScope())
        {
            var request = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>()
                .PrintRequests.Include(r => r.Quote).SingleAsync();
            Assert.Equal(QuoteStatus.PendingReview, request.Status);
            Assert.NotNull(request.Quote);
            Assert.Equal(20.50m, request.Quote.FinalTotalPrice);
            quoteId = request.Quote.Id;
        }

        var withoutCsrf = await client.PostAsync("/Quote/SendEmailQuote", new FormUrlEncodedContent(
            new Dictionary<string, string> { ["requestId"] = requestId.ToString(CultureInfo.InvariantCulture) }));
        Assert.Equal(HttpStatusCode.BadRequest, withoutCsrf.StatusCode);
        var sent = await client.PostAsync("/Quote/SendEmailQuote", HttpTestHelpers.Form(token,
            ("requestId", requestId.ToString(CultureInfo.InvariantCulture))));
        Assert.Equal(HttpStatusCode.ServiceUnavailable, sent.StatusCode);
        Assert.Equal(0, factory.EmailService.QuoteEmailsSent);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var request = await db.PrintRequests.SingleAsync();
            Assert.Equal(QuoteStatus.PendingReview, request.Status);
            request.Status = QuoteStatus.QuoteSent;
            await db.SaveChangesAsync();
        }

        var protector = factory.Services.GetRequiredService<IDataProtectionProvider>()
            .CreateProtector("Quote.Payment.v1").ToTimeLimitedDataProtector();
        var paymentToken = protector.Protect(quoteId.ToString(CultureInfo.InvariantCulture), TimeSpan.FromDays(1));
        using var anonymous = factory.CreateStoreClient();
        var oldStripeClient = Stripe.StripeConfiguration.StripeClient;
        using var handler = new FakeStripeHandler();
        using var fakeHttp = new HttpClient(handler);
        try
        {
            Stripe.StripeConfiguration.StripeClient = new Stripe.StripeClient(
                "sk_test_offline", httpClient: new Stripe.SystemNetHttpClient(fakeHttp, maxNetworkRetries: 0));
            var payment = await anonymous.GetAsync($"/Quote/PayQuote?token={Uri.EscapeDataString(paymentToken)}");
            Assert.Equal(HttpStatusCode.ServiceUnavailable, payment.StatusCode);
            Assert.Null(payment.Headers.Location);
            Assert.True(payment.Headers.CacheControl?.NoStore);
            Assert.Equal(HttpStatusCode.ServiceUnavailable, (await anonymous.GetAsync("/Quote/PayQuote")).StatusCode);
            Assert.Equal(0, handler.RequestCount);
        }
        finally { Stripe.StripeConfiguration.StripeClient = oldStripeClient; }

        using var finalScope = factory.Services.CreateScope();
        var finalRequest = await finalScope.ServiceProvider.GetRequiredService<ApplicationDbContext>()
            .PrintRequests.Include(r => r.Quote).SingleAsync();
        Assert.Equal(QuoteStatus.QuoteSent, finalRequest.Status);
        Assert.Equal(20.50m, finalRequest.Quote!.FinalTotalPrice);
        Assert.Null(finalRequest.Quote.StripePaymentIntentId);
        Assert.Equal(0, factory.EmailService.QuoteEmailsSent);
    }

    private sealed class FakeStripeHandler : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"id\":\"cs_test_offline\",\"object\":\"checkout.session\",\"url\":\"https://checkout.example/session\"}", Encoding.UTF8, "application/json")
            });
        }
    }
}
