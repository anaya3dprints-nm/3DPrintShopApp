using System.Net;
using Xunit;

namespace ThreeDPrintStore.Tests;

public class AdminRouteTests
{
    [Theory]
    [InlineData("/Quote/AdminRequests")]
    [InlineData("/Quote/Details/1")]
    [InlineData("/Order/Index")]
    public async Task Anonymous_admin_gets_redirect_to_login(string path)
    {
        using var factory = new StoreFactory();
        using var client = factory.CreateStoreClient();
        var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Account/Login", response.Headers.Location?.AbsolutePath);
    }

    [Theory]
    [InlineData("/Quote/GenerateQuote")]
    [InlineData("/Quote/SendEmailQuote")]
    [InlineData("/Account/Logout")]
    public async Task Anonymous_admin_posts_do_not_execute(string path)
    {
        using var factory = new StoreFactory();
        using var client = factory.CreateStoreClient();
        var response = await client.PostAsync(path, new FormUrlEncodedContent([]));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Account/Login", response.Headers.Location?.AbsolutePath);
    }

    [Fact]
    public async Task Login_requires_antiforgery_and_valid_credentials_and_rejects_external_return_url()
    {
        using var factory = new StoreFactory();
        using var client = factory.CreateStoreClient();
        var noToken = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(
            new Dictionary<string, string> { ["Username"] = StoreFactory.Username, ["Password"] = StoreFactory.Password }));
        Assert.Equal(HttpStatusCode.BadRequest, noToken.StatusCode);

        var token = await HttpTestHelpers.AntiforgeryToken(client, "/Account/Login");
        var wrong = await client.PostAsync("/Account/Login", HttpTestHelpers.Form(token,
            ("Username", StoreFactory.Username), ("Password", "incorrect")));
        Assert.Equal(HttpStatusCode.OK, wrong.StatusCode);
        Assert.Contains("Invalid username or password", await wrong.Content.ReadAsStringAsync());

        token = await HttpTestHelpers.AntiforgeryToken(client, "/Account/Login");
        var login = await client.PostAsync("/Account/Login", HttpTestHelpers.Form(token,
            ("Username", StoreFactory.Username), ("Password", StoreFactory.Password),
            ("ReturnUrl", "https://attacker.example/")));
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        Assert.Equal("/", login.Headers.Location?.OriginalString);
        Assert.Contains(login.Headers.GetValues("Set-Cookie"), value => value.Contains("secure", StringComparison.OrdinalIgnoreCase));

        factory.InitializeDatabase();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/Quote/AdminRequests")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/Account/Logout", new FormUrlEncodedContent([]))).StatusCode);

        token = await HttpTestHelpers.AntiforgeryToken(client, "/Account/Login");
        Assert.Equal(HttpStatusCode.Redirect, (await client.PostAsync("/Account/Logout", HttpTestHelpers.Form(token))).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await client.GetAsync("/Quote/AdminRequests")).StatusCode);
    }
}
