using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace ThreeDPrintStore.Tests;

public class CartInitializationTests
{
    [Fact]
    public async Task Bootstrap_is_cookie_free_and_cannot_initialize_without_same_origin_fetch()
    {
        using var factory = new StoreFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false, HandleCookies = false
        });
        var page = await client.GetAsync("/Cart/Index");
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        Assert.False(page.Headers.Contains("Set-Cookie"));
        Assert.Contains("no-store", page.Headers.CacheControl!.ToString());
        var html = await page.Content.ReadAsStringAsync();
        Assert.Contains("navigator.locks", html);
        Assert.DoesNotContain("__RequestVerificationToken", html);
        Assert.Contains("<noscript>JavaScript and cookies are required", html);

        foreach (var site in new[] { "", "cross-site", "same-site", "none" })
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/Cart/Initialize");
            if (site != "") request.Headers.Add("Sec-Fetch-Site", site);
            request.Headers.Add("X-Cart-Initialization", "1");
            using var response = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.False(response.Headers.Contains("Set-Cookie"));
        }
        using var navigation = await client.GetAsync("/Cart/Initialize");
        Assert.Equal(HttpStatusCode.MethodNotAllowed, navigation.StatusCode);
        Assert.False(navigation.Headers.Contains("Set-Cookie"));
    }

    [Fact]
    public async Task Missing_or_blocked_cookies_fail_verification_and_existing_identity_is_not_overwritten()
    {
        using var factory = new StoreFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false, HandleCookies = false
        });
        static HttpRequestMessage Request(string path, string? cookie = null)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, path);
            request.Headers.Add("Sec-Fetch-Site", "same-origin");
            request.Headers.Add("X-Cart-Initialization", "1");
            if (cookie != null) request.Headers.Add("Cookie", cookie);
            return request;
        }
        using var initialized = await client.SendAsync(Request("/Cart/Initialize"));
        Assert.Equal(HttpStatusCode.NoContent, initialized.StatusCode);
        var headers = initialized.Headers.GetValues("Set-Cookie").ToArray();
        var identity = Assert.Single(headers, h => h.StartsWith("StoreCartIdentity="));
        Assert.Contains("secure", identity, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("httponly", identity, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", identity, StringComparison.OrdinalIgnoreCase);
        using var blocked = await client.SendAsync(Request("/Cart/InitializationStatus"));
        Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);
        using var identityOnly = new HttpRequestMessage(HttpMethod.Get, "/Cart/Index");
        identityOnly.Headers.Add("Cookie", identity.Split(';')[0]);
        using var retry = await client.SendAsync(identityOnly);
        Assert.False(retry.Headers.Contains("Set-Cookie"));
        Assert.Contains("navigator.locks", await retry.Content.ReadAsStringAsync());
        var cookies = string.Join("; ", headers.Select(h => h.Split(';')[0]));
        using var status = await client.SendAsync(Request("/Cart/InitializationStatus", cookies));
        Assert.Equal(HttpStatusCode.NoContent, status.StatusCode);
        using var repeated = await client.SendAsync(Request("/Cart/Initialize", cookies));
        Assert.Equal(HttpStatusCode.NoContent, repeated.StatusCode);
        Assert.False(repeated.Headers.TryGetValues("Set-Cookie", out var repeatedHeaders) &&
                     repeatedHeaders.Any(h => h.StartsWith("StoreCartIdentity=")));
        using var invalid = await client.SendAsync(Request("/Cart/Initialize", "StoreCartIdentity=invalid"));
        Assert.Equal(HttpStatusCode.Conflict, invalid.StatusCode);
        Assert.False(invalid.Headers.Contains("Set-Cookie"));
    }

    [Fact]
    public async Task Initializing_an_existing_cart_session_does_not_rewrite_session()
    {
        using var factory = new StoreFactory();
        using var client = factory.CreateStoreClient();
        factory.InitializeDatabase();
        var token = await HttpTestHelpers.AntiforgeryToken(client, "/Home/Catalog");
        using var added = await client.PostAsync("/Cart/AddAjax", HttpTestHelpers.Form(token, ("productId", "1")));
        Assert.Equal(HttpStatusCode.OK, added.StatusCode);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/Cart/Initialize");
        request.Headers.Add("Sec-Fetch-Site", "same-origin");
        request.Headers.Add("X-Cart-Initialization", "1");
        using var initialized = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.NoContent, initialized.StatusCode);
        Assert.DoesNotContain(initialized.Headers.GetValues("Set-Cookie"),
            h => h.Contains("Session", StringComparison.OrdinalIgnoreCase));
        using var status = new HttpRequestMessage(HttpMethod.Post, "/Cart/InitializationStatus");
        status.Headers.Add("Sec-Fetch-Site", "same-origin");
        status.Headers.Add("X-Cart-Initialization", "1");
        Assert.Equal(HttpStatusCode.NoContent, (await client.SendAsync(status)).StatusCode);
        Assert.Contains("La Llorona Duck", await client.GetStringAsync("/Cart/Index"));
    }
}
