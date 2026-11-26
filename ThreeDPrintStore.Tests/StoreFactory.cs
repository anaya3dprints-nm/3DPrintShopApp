using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;
using Stripe.Checkout;
using ThreeDPrintStore.Controllers;
using ThreeDPrintStore.Data;
using ThreeDPrintStore.Models;
using ThreeDPrintStore.Services;
using Xunit;

[assembly: WebApplicationFactoryContentRoot(
    "ThreeDPrintStore, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null",
    "../../../../ThreeDPrintStore", "ThreeDPrintStore.csproj", "0")]

namespace ThreeDPrintStore.Tests;

public sealed class StoreFactory : WebApplicationFactory<CartController>
{
    public const string Username = "test-admin";
    public const string Password = "test-only-password";
    private readonly string _dataDirectory = Path.Combine(AppContext.BaseDirectory, "test-data", Guid.NewGuid().ToString("N"));
    public List<SessionCreateOptions> CheckoutRequests { get; } = new();
    public RecordingEmailService EmailService { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        Directory.CreateDirectory(_dataDirectory);
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = $"Data Source={Path.Combine(_dataDirectory, "store.db")};Pooling=False",
            ["Admin:Username"] = Username,
            ["Admin:PasswordHash"] = new PasswordHasher<string>().HashPassword(Username, Password),
            ["Store:PublicOrigin"] = "https://shop.example",
            ["Stripe:WebhookSecret"] = "whsec_test_only"
        }));
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IEmailService>();
            services.AddSingleton<IEmailService>(EmailService);
            foreach (var descriptor in services.Where(d => d.ServiceType == typeof(IHostedService)
                         && d.ImplementationType == typeof(OrderNotificationWorker)).ToList())
                services.Remove(descriptor);
            services.AddControllersWithViews().AddControllersAsServices();
            services.Replace(ServiceDescriptor.Transient<CartController>(provider =>
                new FakeCartController(
                    provider.GetRequiredService<ApplicationDbContext>(),
                    provider.GetRequiredService<IConfiguration>(),
                    provider.GetRequiredService<IWebHostEnvironment>(),
                    provider.GetRequiredService<ILogger<CartController>>(), CheckoutRequests)));
        });
    }

    public HttpClient CreateStoreClient() => CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost"),
        AllowAutoRedirect = false,
        HandleCookies = true
    });

    public void InitializeDatabase()
    {
        using var scope = Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.EnsureCreated();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing && Directory.Exists(_dataDirectory))
            Directory.Delete(_dataDirectory, recursive: true);
    }

    public sealed class RecordingEmailService : IEmailService
    {
        public int QuoteEmailsSent { get; private set; }
        public Task SendEmailAsync(PrintRequest request, PrintQuote quote, string stripeCheckoutUrl)
        {
            QuoteEmailsSent++;
            return Task.CompletedTask;
        }
        public Task SendSubmissionConfirmationAsync(PrintRequest request) => Task.CompletedTask;
    }

    private sealed class FakeCartController(
        ApplicationDbContext db, IConfiguration configuration, IWebHostEnvironment environment,
        ILogger<CartController> logger, List<SessionCreateOptions> requests)
        : CartController(db, configuration, environment, logger)
    {
        protected override Task<Session> CreateSessionAsync(SessionCreateOptions options, int orderId)
        {
            requests.Add(options);
            return Task.FromResult(new Session { Id = $"cs_test_{orderId}", Url = "https://checkout.example/session" });
        }
    }
}

internal static class HttpTestHelpers
{
    public static async Task<string> AntiforgeryToken(HttpClient client, string path)
    {
        var response = await client.GetAsync(path);
        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync();
        var match = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        Assert.True(match.Success, $"Antiforgery token not found on {path}");
        return WebUtility.HtmlDecode(match.Groups[1].Value);
    }

    public static FormUrlEncodedContent Form(string token, params (string Key, string Value)[] fields) =>
        new(fields.Append((Key: "__RequestVerificationToken", Value: token))
            .Select(field => new KeyValuePair<string, string>(field.Key, field.Value)));
}
