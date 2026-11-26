using System.Collections.Concurrent;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Stripe;
using ThreeDPrintStore.Data;
using ThreeDPrintStore.Models;
using Xunit;

namespace ThreeDPrintStore.Tests;

[Collection("Stripe isolation")]
public class DuplicateCheckoutTests
{
    [Fact]
    public async Task Concurrent_posts_of_the_same_form_never_create_two_orders_or_stripe_sessions()
    {
        using var stripe = new FakeStripe { HoldFirstCreate = true };
        using var factory = stripe.Factory();
        using var client = factory.CreateStoreClient();
        factory.InitializeDatabase();
        var (csrf, attempt) = await PrepareCart(client);

        var first = Checkout(client, csrf, attempt);
        await stripe.FirstCreateEntered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var second = Checkout(client, csrf, attempt);
        try
        {
            await Task.Delay(150);
        }
        finally { stripe.ReleaseFirstCreate.TrySetResult(); }
        var responses = await Task.WhenAll(first, second);

        Assert.All(responses, response => Assert.Contains(response.StatusCode,
            new[] { HttpStatusCode.Redirect, HttpStatusCode.Conflict, HttpStatusCode.ServiceUnavailable }));
        Assert.Contains(responses, response => response.StatusCode == HttpStatusCode.Redirect);
        Assert.Single(stripe.CreatedKeys.Distinct());
        using var scope = factory.Services.CreateScope();
        Assert.Single(await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Orders.ToListAsync());
    }

    [Fact]
    public async Task Concurrent_distinct_forms_from_one_browser_share_the_same_active_order()
    {
        using var stripe = new FakeStripe { HoldFirstCreate = true };
        using var factory = stripe.Factory();
        using var client = factory.CreateStoreClient();
        factory.InitializeDatabase();
        var (csrf, firstAttempt) = await PrepareCart(client);
        var (otherCsrf, secondAttempt) = await ReadForm(client);
        Assert.NotEqual(firstAttempt, secondAttempt);
        var first = Checkout(client, csrf, firstAttempt);
        await stripe.FirstCreateEntered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var second = Checkout(client, otherCsrf, secondAttempt);
        try { await Task.Delay(150); }
        finally { stripe.ReleaseFirstCreate.TrySetResult(); }
        var responses = await Task.WhenAll(first, second);
        Assert.Contains(responses, response => response.StatusCode == HttpStatusCode.Redirect);
        Assert.All(responses, response => Assert.Contains(response.StatusCode,
            new[] { HttpStatusCode.Redirect, HttpStatusCode.Conflict, HttpStatusCode.ServiceUnavailable }));
        Assert.Single(stripe.CreatedKeys.Distinct());
        using var scope = factory.Services.CreateScope();
        Assert.Single(await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Orders.ToListAsync());
    }

    [Fact]
    public async Task Parallel_first_cart_views_for_populated_session_cannot_create_distinct_checkouts()
    {
        using var stripe = new FakeStripe();
        using var factory = stripe.Factory();
        factory.InitializeDatabase();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false, HandleCookies = false
        });
        var cookies = new Dictionary<string, string>();
        await InitializeWithCookies(client, cookies);
        var initial = await SendWithCookies(client, HttpMethod.Get, "/Cart/Index", cookies);
        initial.EnsureSuccessStatusCode();
        ApplyCookies(cookies, initial);
        var csrf = Field(await initial.Content.ReadAsStringAsync(), "__RequestVerificationToken");
        var added = await SendWithCookies(client, HttpMethod.Post, "/Cart/AddAjax", cookies,
            HttpTestHelpers.Form(csrf, ("productId", "1")));
        Assert.Equal(HttpStatusCode.OK, added.StatusCode);
        ApplyCookies(cookies, added);
        Assert.Contains(cookies.Keys, name => name.Contains("Session", StringComparison.OrdinalIgnoreCase));
        var oldIdentity = cookies["StoreCartIdentity"];
        cookies.Remove("StoreCartIdentity");

        var pages = await Task.WhenAll(
            SendWithCookies(client, HttpMethod.Get, "/Cart/Index", cookies),
            SendWithCookies(client, HttpMethod.Get, "/Cart/Index", cookies));
        foreach (var page in pages)
        {
            Assert.DoesNotContain("Set-Cookie", page.Headers.Select(h => h.Key));
            Assert.Contains("navigator.locks", await page.Content.ReadAsStringAsync());
        }
        await InitializeWithCookies(client, cookies);
        Assert.Equal(oldIdentity, cookies["StoreCartIdentity"]);
        pages = await Task.WhenAll(
            SendWithCookies(client, HttpMethod.Get, "/Cart/Index", cookies),
            SendWithCookies(client, HttpMethod.Get, "/Cart/Index", cookies));
        var forms = new List<(Dictionary<string, string> Cookies, string Csrf, CheckoutAttempt Attempt)>();
        foreach (var page in pages)
        {
            page.EnsureSuccessStatusCode();
            var tabCookies = new Dictionary<string, string>(cookies);
            ApplyCookies(tabCookies, page);
            var html = await page.Content.ReadAsStringAsync();
            forms.Add((tabCookies, Field(html, "__RequestVerificationToken"),
                new CheckoutAttempt(Field(html, "checkoutAttempt"), Field(html, "cartBinding"))));
        }
        Assert.True(forms[0].Cookies.ContainsKey("StoreCartIdentity"));
        Assert.Equal(forms[0].Cookies["StoreCartIdentity"], forms[1].Cookies["StoreCartIdentity"]);

        var responses = await Task.WhenAll(forms.Select(form =>
            SendWithCookies(client, HttpMethod.Post, "/Cart/Checkout", form.Cookies,
                HttpTestHelpers.Form(form.Csrf, ("checkoutAttempt", form.Attempt.Token),
                    ("cartBinding", form.Attempt.CartBinding)))));
        Assert.Contains(responses, response => response.StatusCode == HttpStatusCode.Redirect);
        Assert.All(responses, response => Assert.Contains(response.StatusCode,
            new[] { HttpStatusCode.Redirect, HttpStatusCode.Conflict, HttpStatusCode.ServiceUnavailable }));
        Assert.Single(stripe.CreatedKeys.Distinct());
        using var scope = factory.Services.CreateScope();
        Assert.Single(await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Orders.ToListAsync());
    }

    [Fact]
    public async Task Delayed_cookieless_first_views_cannot_overwrite_paid_successor_identity()
    {
        using var stripe = new FakeStripe();
        using var factory = stripe.Factory();
        factory.HoldFirstCartGet = true;
        factory.InitializeDatabase();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
            HandleCookies = false
        });
        var cookies = new Dictionary<string, string>();
        var delayed = SendWithCookies(client, HttpMethod.Get, "/Cart/Index", cookies);
        await factory.FirstCartGetEntered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        try
        {
            var firstView = await SendWithCookies(client, HttpMethod.Get, "/Cart/Index", cookies);
            Assert.Equal(HttpStatusCode.OK, firstView.StatusCode);
            Assert.False(firstView.Headers.Contains("Set-Cookie"));
            Assert.Contains("navigator.locks", await firstView.Content.ReadAsStringAsync());

            await InitializeWithCookies(client, cookies);
            var identity = cookies["StoreCartIdentity"];
            var page = await SendWithCookies(client, HttpMethod.Get, "/Cart/Index", cookies);
            ApplyCookies(cookies, page);
            var csrf = Field(await page.Content.ReadAsStringAsync(), "__RequestVerificationToken");
            var add = await SendWithCookies(client, HttpMethod.Post, "/Cart/AddAjax", cookies,
                HttpTestHelpers.Form(csrf, ("productId", "1")));
            Assert.Equal(HttpStatusCode.OK, add.StatusCode);
            ApplyCookies(cookies, add);
            page = await SendWithCookies(client, HttpMethod.Get, "/Cart/Index", cookies);
            var html = await page.Content.ReadAsStringAsync();
            var attempt = new CheckoutAttempt(Field(html, "checkoutAttempt"), Field(html, "cartBinding"));
            var checkout = await SendWithCookies(client, HttpMethod.Post, "/Cart/Checkout", cookies,
                HttpTestHelpers.Form(csrf, ("checkoutAttempt", attempt.Token), ("cartBinding", attempt.CartBinding)));
            Assert.Equal(HttpStatusCode.Redirect, checkout.StatusCode);
            Order order;
            using (var scope = factory.Services.CreateScope())
                order = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Orders.AsNoTracking().SingleAsync();
            Assert.Equal(HttpStatusCode.OK, (await SendPaidWebhook(client, order)).StatusCode);
            var rotate = await SendWithCookies(client, HttpMethod.Post, "/Cart/StartNewOrder", cookies,
                HttpTestHelpers.Form(csrf, ("checkoutAttempt", attempt.Token), ("cartBinding", attempt.CartBinding)));
            Assert.Equal(HttpStatusCode.Redirect, rotate.StatusCode);
            ApplyCookies(cookies, rotate);
            Assert.NotEqual(identity, cookies["StoreCartIdentity"]);
            var successor = cookies["StoreCartIdentity"];

            // Release a genuinely delayed cookie-less first response after rotation.
            factory.ReleaseFirstCartGet.TrySetResult();
            var delayedResponse = await delayed;
            Assert.False(delayedResponse.Headers.Contains("Set-Cookie"));
            Assert.Contains("navigator.locks", await delayedResponse.Content.ReadAsStringAsync());
            ApplyCookies(cookies, delayedResponse);
            await InitializeWithCookies(client, cookies);
            Assert.Equal(successor, cookies["StoreCartIdentity"]);
            page = await SendWithCookies(client, HttpMethod.Get, "/Cart/Index", cookies);
            html = await page.Content.ReadAsStringAsync();
            var next = new CheckoutAttempt(Field(html, "checkoutAttempt"), Field(html, "cartBinding"));
            checkout = await SendWithCookies(client, HttpMethod.Post, "/Cart/Checkout", cookies,
                HttpTestHelpers.Form(csrf, ("checkoutAttempt", next.Token), ("cartBinding", next.CartBinding)));
            Assert.Equal(HttpStatusCode.Redirect, checkout.StatusCode);
            Assert.Equal(2, stripe.CreatedKeys.Distinct().Count());
        }
        finally { factory.ReleaseFirstCartGet.TrySetResult(); }
    }

    [Fact]
    public async Task Lost_stripe_response_retries_same_order_and_same_idempotency_key()
    {
        using var stripe = new FakeStripe { LoseFirstCreateResponse = true };
        using var factory = stripe.Factory();
        using var client = factory.CreateStoreClient();
        factory.InitializeDatabase();
        var (csrf, attempt) = await PrepareCart(client);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await Checkout(client, csrf, attempt)).StatusCode);
        var retry = await Checkout(client, csrf, attempt);
        Assert.Equal(HttpStatusCode.Redirect, retry.StatusCode);
        Assert.Equal(2, stripe.CreatedKeys.Count);
        Assert.Single(stripe.CreatedKeys.Distinct());
        Assert.Equal(stripe.CreateBodies.First(), stripe.CreateBodies.Last());
        using var scope = factory.Services.CreateScope();
        var order = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Orders.SingleAsync();
        Assert.Equal($"cart-order-{order.Id}", stripe.CreatedKeys.First());
        Assert.Equal("cs_test_1", order.StripeSessionId);
    }

    [Fact]
    public async Task Uncertain_creation_past_idempotency_retention_fails_closed()
    {
        using var stripe = new FakeStripe { LoseFirstCreateResponse = true };
        using var factory = stripe.Factory();
        using var client = factory.CreateStoreClient();
        factory.InitializeDatabase();
        var (csrf, attempt) = await PrepareCart(client);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await Checkout(client, csrf, attempt)).StatusCode);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var order = await db.Orders.SingleAsync();
            order.CreatedAtUtc = DateTime.UtcNow.AddHours(-25);
            await db.SaveChangesAsync();
        }
        var retry = await Checkout(client, csrf, attempt);
        Assert.NotEqual(HttpStatusCode.Redirect, retry.StatusCode);
        Assert.Single(stripe.CreatedKeys);
        using var finalScope = factory.Services.CreateScope();
        Assert.Single(await finalScope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Orders.ToListAsync());
    }

    [Fact]
    public async Task Browser_retries_and_return_from_cancel_reuse_open_session_but_changed_cart_is_blocked()
    {
        using var stripe = new FakeStripe();
        using var factory = stripe.Factory();
        using var client = factory.CreateStoreClient();
        factory.InitializeDatabase();
        var (csrf, attempt) = await PrepareCart(client);
        var first = await Checkout(client, csrf, attempt);
        Assert.Equal(HttpStatusCode.Redirect, first.StatusCode);
        var replay = await Checkout(client, csrf, attempt);
        Assert.Equal(first.Headers.Location, replay.Headers.Location);
        var (freshCsrf, freshAttempt) = await ReadForm(client);
        Assert.NotEqual(attempt, freshAttempt);
        var fromCancel = await Checkout(client, freshCsrf, freshAttempt);
        Assert.Equal(first.Headers.Location, fromCancel.Headers.Location);
        Assert.Single(stripe.CreatedKeys.Distinct());

        var changedOptions = await Checkout(client, freshCsrf, freshAttempt,
            ("includeCacheDrop", "true"), ("sponsorName", "Different"));
        Assert.Equal(HttpStatusCode.Conflict, changedOptions.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync("/Cart/AddAjax",
            HttpTestHelpers.Form(freshCsrf, ("productId", "2")))).StatusCode);
        var (newCsrf, newAttempt) = await ReadForm(client);
        Assert.Equal(HttpStatusCode.Conflict, (await Checkout(client, newCsrf, newAttempt)).StatusCode);
        using var scope = factory.Services.CreateScope();
        Assert.Single(await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Orders.ToListAsync());
    }

    [Fact]
    public async Task Old_form_cannot_checkout_cart_changed_in_another_tab()
    {
        using var stripe = new FakeStripe();
        using var factory = stripe.Factory();
        using var client = factory.CreateStoreClient();
        factory.InitializeDatabase();
        var (csrf, staleAttempt) = await PrepareCart(client);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync("/Cart/AddAjax",
            HttpTestHelpers.Form(csrf, ("productId", "2")))).StatusCode);

        Assert.Equal(HttpStatusCode.Conflict, (await Checkout(client, csrf, staleAttempt)).StatusCode);
        Assert.Empty(stripe.CreatedKeys);
        var (newCsrf, newAttempt) = await ReadForm(client);
        Assert.Equal(HttpStatusCode.Redirect, (await Checkout(client, newCsrf, newAttempt)).StatusCode);
        using var scope = factory.Services.CreateScope();
        var order = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Orders
            .Include(o => o.Items).SingleAsync();
        Assert.Equal(new[] { 1, 2 }, order.Items.Select(i => i.ProductId).OrderBy(id => id).ToArray());
    }

    [Fact]
    public async Task Expired_unpaid_session_requires_fresh_form_before_new_order()
    {
        using var stripe = new FakeStripe();
        using var factory = stripe.Factory();
        using var client = factory.CreateStoreClient();
        factory.InitializeDatabase();
        var (csrf, attempt) = await PrepareCart(client);
        Assert.Equal(HttpStatusCode.Redirect, (await Checkout(client, csrf, attempt)).StatusCode);
        stripe.SessionStatus = "expired";
        Assert.Equal(HttpStatusCode.Conflict, (await Checkout(client, csrf, attempt)).StatusCode);
        stripe.SessionStatus = "open";
        var (newCsrf, newAttempt) = await ReadForm(client);
        var fresh = await Checkout(client, newCsrf, newAttempt);
        Assert.Equal(HttpStatusCode.Redirect, fresh.StatusCode);
        Assert.Equal(2, stripe.CreatedKeys.Distinct().Count());
        using var scope = factory.Services.CreateScope();
        Assert.Equal(2, await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Orders.CountAsync());
    }

    [Theory]
    [InlineData("complete", "paid")]
    [InlineData("complete", "unpaid")]
    [InlineData("expired", "paid")]
    [InlineData("unknown", "unpaid")]
    public async Task Non_open_or_ambiguous_stripe_session_never_creates_a_replacement(string status, string paymentStatus)
    {
        using var stripe = new FakeStripe();
        using var factory = stripe.Factory();
        using var client = factory.CreateStoreClient();
        factory.InitializeDatabase();
        var (csrf, attempt) = await PrepareCart(client);
        Assert.Equal(HttpStatusCode.Redirect, (await Checkout(client, csrf, attempt)).StatusCode);
        stripe.SessionStatus = status;
        stripe.PaymentStatus = paymentStatus;
        var (newCsrf, newAttempt) = await ReadForm(client);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await Checkout(client, newCsrf, newAttempt)).StatusCode);
        Assert.Single(stripe.CreatedKeys);
        using var scope = factory.Services.CreateScope();
        Assert.Single(await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Orders.ToListAsync());
    }

    [Fact]
    public async Task Failed_session_lookup_does_not_open_another_checkout()
    {
        using var stripe = new FakeStripe();
        using var factory = stripe.Factory();
        using var client = factory.CreateStoreClient();
        factory.InitializeDatabase();
        var (csrf, attempt) = await PrepareCart(client);
        Assert.Equal(HttpStatusCode.Redirect, (await Checkout(client, csrf, attempt)).StatusCode);
        stripe.FailLookup = true;
        var (newCsrf, newAttempt) = await ReadForm(client);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await Checkout(client, newCsrf, newAttempt)).StatusCode);
        Assert.Single(stripe.CreatedKeys);
        using var scope = factory.Services.CreateScope();
        Assert.Single(await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Orders.ToListAsync());
    }

    [Fact]
    public async Task Separate_browsers_can_purchase_identical_carts()
    {
        using var stripe = new FakeStripe();
        using var factory = stripe.Factory();
        using var first = factory.CreateStoreClient();
        using var second = factory.CreateStoreClient();
        factory.InitializeDatabase();
        var (firstCsrf, firstAttempt) = await PrepareCart(first);
        var (secondCsrf, secondAttempt) = await PrepareCart(second);
        Assert.Equal(HttpStatusCode.Redirect, (await Checkout(first, firstCsrf, firstAttempt)).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await Checkout(second, secondCsrf, secondAttempt)).StatusCode);
        Assert.Equal(2, stripe.CreatedKeys.Distinct().Count());
        using var scope = factory.Services.CreateScope();
        Assert.Equal(2, await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Orders.CountAsync());
    }

    [Fact]
    public async Task Unpaid_order_cannot_be_bypassed_by_start_new_or_a_mismatched_form_binding()
    {
        using var stripe = new FakeStripe();
        using var factory = stripe.Factory();
        using var client = factory.CreateStoreClient();
        factory.InitializeDatabase();
        var (csrf, attempt) = await PrepareCart(client);
        var (_, otherAttempt) = await ReadForm(client);
        Assert.Equal(HttpStatusCode.Redirect, (await Checkout(client, csrf, attempt)).StatusCode);
        var noCsrf = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["checkoutAttempt"] = attempt.Token, ["cartBinding"] = attempt.CartBinding
        });
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/Cart/StartNewOrder", noCsrf)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsync("/Cart/StartNewOrder",
            HttpTestHelpers.Form(csrf, ("checkoutAttempt", attempt.Token), ("cartBinding", attempt.CartBinding)))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Checkout(client, csrf,
            new CheckoutAttempt(otherAttempt.Token, attempt.CartBinding))).StatusCode);
        Assert.Single(stripe.CreatedKeys);
        using var scope = factory.Services.CreateScope();
        Assert.Single(await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Orders.ToListAsync());
    }

    [Fact]
    public async Task Paid_webhook_blocks_stale_and_refreshed_forms_for_unchanged_cart()
    {
        using var stripe = new FakeStripe();
        using var factory = stripe.Factory();
        using var client = factory.CreateStoreClient();
        factory.InitializeDatabase();
        var (csrf, attempt) = await PrepareCart(client);
        var (staleCsrf, unsubmittedAttempt) = await ReadForm(client);
        Assert.Equal(HttpStatusCode.Redirect, (await Checkout(client, csrf, attempt)).StatusCode);
        Order order;
        using (var scope = factory.Services.CreateScope())
            order = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Orders.AsNoTracking().SingleAsync();
        Assert.Equal(HttpStatusCode.OK, (await SendPaidWebhook(client, order)).StatusCode);
        Assert.NotEqual(HttpStatusCode.Redirect, (await Checkout(client, csrf, attempt)).StatusCode);
        var (newCsrf, newAttempt) = await ReadForm(client);
        Assert.NotEqual(attempt, newAttempt);
        Assert.NotEqual(HttpStatusCode.Redirect, (await Checkout(client, newCsrf, newAttempt)).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await client.PostAsync("/Cart/StartNewOrder",
            HttpTestHelpers.Form(newCsrf, ("checkoutAttempt", newAttempt.Token),
                ("cartBinding", newAttempt.CartBinding)))).StatusCode);
        Assert.NotEqual(HttpStatusCode.Redirect, (await Checkout(client, staleCsrf, unsubmittedAttempt)).StatusCode);
        using (var beforeNewOrder = factory.Services.CreateScope())
            Assert.Single(await beforeNewOrder.ServiceProvider.GetRequiredService<ApplicationDbContext>().Orders.ToListAsync());
        var (freshCsrf, freshAttempt) = await ReadForm(client);
        Assert.Equal(HttpStatusCode.Redirect, (await Checkout(client, freshCsrf, freshAttempt)).StatusCode);
        using var finalScope = factory.Services.CreateScope();
        var orders = await finalScope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Orders.OrderBy(o => o.Id).ToListAsync();
        Assert.Equal(2, orders.Count);
        Assert.Equal(OrderStatus.Paid, orders[0].Status);
        Assert.Equal(OrderStatus.PendingPayment, orders[1].Status);
        Assert.Equal(2, stripe.CreatedKeys.Distinct().Count());
    }

    private record CheckoutAttempt(string Token, string CartBinding);

    private static async Task<(string Csrf, CheckoutAttempt Attempt)> PrepareCart(HttpClient client)
    {
        var csrf = await HttpTestHelpers.AntiforgeryToken(client, "/Cart/Index");
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync("/Cart/AddAjax",
            HttpTestHelpers.Form(csrf, ("productId", "1")))).StatusCode);
        return await ReadForm(client);
    }

    private static async Task<(string Csrf, CheckoutAttempt Attempt)> ReadForm(HttpClient client)
    {
        var html = await client.GetStringAsync("/Cart/Index");
        return (Field(html, "__RequestVerificationToken"), new CheckoutAttempt(
            Field(html, "checkoutAttempt"), Field(html, "cartBinding")));
    }

    private static string Field(string html, string name)
    {
        var match = Regex.Match(html, $"name=\"{name}\"[^>]*value=\"([^\"]+)\"");
        Assert.True(match.Success, $"Missing {name} field");
        return WebUtility.HtmlDecode(match.Groups[1].Value);
    }

    private static Task<HttpResponseMessage> SendWithCookies(HttpClient client, HttpMethod method, string path,
        Dictionary<string, string> cookies, HttpContent? content = null)
    {
        var request = new HttpRequestMessage(method, path) { Content = content };
        request.Headers.TryAddWithoutValidation("Cookie", string.Join("; ", cookies.Select(c => $"{c.Key}={c.Value}")));
        return client.SendAsync(request);
    }

    private static void ApplyCookies(Dictionary<string, string> cookies, HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("Set-Cookie", out var headers)) return;
        foreach (var header in headers)
        {
            var pair = header.Split(';', 2)[0].Split('=', 2);
            cookies[pair[0]] = pair[1];
        }
    }

    private static async Task InitializeWithCookies(HttpClient client, Dictionary<string, string> cookies)
    {
        foreach (var path in new[] { "/Cart/Initialize", "/Cart/InitializationStatus" })
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, path);
            request.Headers.TryAddWithoutValidation("Cookie", string.Join("; ", cookies.Select(c => $"{c.Key}={c.Value}")));
            request.Headers.Add("Sec-Fetch-Site", "same-origin");
            request.Headers.Add("X-Cart-Initialization", "1");
            using var response = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
            ApplyCookies(cookies, response);
        }
    }

    private static Task<HttpResponseMessage> Checkout(HttpClient client, string csrf, CheckoutAttempt attempt,
        params (string Key, string Value)[] fields) =>
        client.PostAsync("/Cart/Checkout", HttpTestHelpers.Form(csrf,
            fields.Append(("checkoutAttempt", attempt.Token)).Append(("cartBinding", attempt.CartBinding)).ToArray()));

    private static Task<HttpResponseMessage> SendPaidWebhook(HttpClient client, Order order)
    {
        var payload = JsonSerializer.Serialize(new
        {
            id = "evt_duplicate_test", @object = "event", api_version = StripeConfiguration.ApiVersion,
            type = "checkout.session.completed",
            data = new { @object = new
            {
                id = order.StripeSessionId, @object = "checkout.session", mode = "payment", payment_status = "paid",
                currency = "usd", amount_total = order.TotalCents, payment_intent = "pi_test_1",
                metadata = new Dictionary<string, string> { ["order_id"] = order.Id.ToString(), ["order_token"] = order.CheckoutToken },
                customer_details = new { email = "buyer@example.com" },
                collected_information = new { shipping_details = new
                {
                    name = "Buyer", address = new { line1 = "1 Test St", city = "Albuquerque", state = "NM", postal_code = "87101", country = "US" }
                } }
            } }
        });
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var hash = HMACSHA256.HashData(Encoding.UTF8.GetBytes("whsec_test_only"), Encoding.UTF8.GetBytes($"{timestamp}.{payload}"));
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/webhook/stripe")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        request.Headers.Add("Stripe-Signature", $"t={timestamp},v1={Convert.ToHexString(hash).ToLowerInvariant()}");
        return client.SendAsync(request);
    }

    private sealed class FakeStripe : HttpMessageHandler
    {
        private readonly IStripeClient _previous = StripeConfiguration.StripeClient;
        private readonly HttpClient _http;
        private readonly ConcurrentDictionary<string, string> _sessions = new();
        private int _createCount;
        public ConcurrentQueue<string> CreatedKeys { get; } = new();
        public ConcurrentQueue<string> CreateBodies { get; } = new();
        public bool LoseFirstCreateResponse { get; init; }
        public bool HoldFirstCreate { get; init; }
        public string SessionStatus { get; set; } = "open";
        public string PaymentStatus { get; set; } = "unpaid";
        public bool FailLookup { get; set; }
        public TaskCompletionSource FirstCreateEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ReleaseFirstCreate { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public FakeStripe()
        {
            _http = new HttpClient(this, disposeHandler: false);
            StripeConfiguration.StripeClient = new StripeClient("sk_test_offline",
                httpClient: new SystemNetHttpClient(_http, maxNetworkRetries: 0));
        }

        public StoreFactory Factory() => new() { UseStripeTransport = true };

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string id;
            if (request.Method == HttpMethod.Post)
            {
                var key = request.Headers.GetValues("Idempotency-Key").Single();
                CreatedKeys.Enqueue(key);
                CreateBodies.Enqueue(await request.Content!.ReadAsStringAsync(cancellationToken));
                id = _sessions.GetOrAdd(key, _ => $"cs_test_{_sessions.Count + 1}");
                var count = Interlocked.Increment(ref _createCount);
                if (count == 1 && HoldFirstCreate)
                {
                    FirstCreateEntered.TrySetResult();
                    await ReleaseFirstCreate.Task.WaitAsync(cancellationToken);
                }
                if (count == 1 && LoseFirstCreateResponse) throw new HttpRequestException("Simulated lost Stripe response");
            }
            else
            {
                if (FailLookup) throw new HttpRequestException("Simulated Stripe lookup failure");
                id = request.RequestUri!.Segments.Last();
            }
            var json = JsonSerializer.Serialize(new
            {
                id, @object = "checkout.session", url = $"https://checkout.example/{id}",
                status = SessionStatus, payment_status = PaymentStatus
            });
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                StripeConfiguration.StripeClient = _previous;
                _http.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
