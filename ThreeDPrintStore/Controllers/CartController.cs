using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Stripe.Checkout;
using ThreeDPrintStore.Data;
using ThreeDPrintStore.Models;
using ThreeDPrintStore.Services;

namespace ThreeDPrintStore.Controllers;

public class CartController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<CartController> _logger;

    public CartController(ApplicationDbContext db, IConfiguration configuration, IWebHostEnvironment environment, ILogger<CartController> logger)
        => (_db, _configuration, _environment, _logger) = (db, configuration, environment, logger);

    private List<CartItem> ReadCart()
    {
        try
        {
            var stored = JsonSerializer.Deserialize<List<CartItem>>(HttpContext.Session.GetString("Cart") ?? "[]") ?? new();
            var result = new List<CartItem>();
            foreach (var item in stored)
            {
                var product = ProductCatalog.Find(item.ProductId);
                if (product == null || item.Quantity < 1 || item.Quantity > product.MaxQuantity ||
                    result.Any(i => i.ProductId == item.ProductId))
                    continue;
                result.Add(new CartItem { ProductId = product.Id, Name = product.Name, Price = product.PriceCents / 100m, Quantity = item.Quantity });
            }
            return result;
        }
        catch (JsonException) { return new(); }
    }

    private void SaveCart(List<CartItem> cart) => HttpContext.Session.SetString("Cart", JsonSerializer.Serialize(cart));

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult AddAjax(int productId)
    {
        var product = ProductCatalog.Find(productId);
        if (product == null) return BadRequest("Unknown product.");
        var cart = ReadCart();
        var item = cart.FirstOrDefault(i => i.ProductId == productId);
        if (item?.Quantity >= product.MaxQuantity) return BadRequest("Product quantity limit reached.");
        if (item == null)
            cart.Add(new CartItem { ProductId = product.Id, Name = product.Name, Price = product.PriceCents / 100m, Quantity = 1 });
        else item.Quantity++;
        SaveCart(cart);
        return Json(new { success = true, cartCount = cart.Sum(i => i.Quantity) });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Update(int productId, int quantity)
    {
        var product = ProductCatalog.Find(productId);
        if (product == null || quantity < 0 || quantity > product.MaxQuantity) return BadRequest("Invalid quantity.");
        var cart = ReadCart();
        var item = cart.FirstOrDefault(i => i.ProductId == productId);
        if (item == null) return BadRequest("Item not in cart.");
        if (quantity == 0) cart.Remove(item);
        else item.Quantity = quantity;
        SaveCart(cart);
        return RedirectToAction(nameof(Index));
    }

    private const string CartCookie = "StoreCartIdentity";
    private static string NewToken() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    private static bool ValidToken(string? token) =>
        token is { Length: 64 } && token.All(c => c is >= '0' and <= '9' or >= 'A' and <= 'F');
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private static string CartFingerprint(List<CartItem> cart) =>
        Hash(JsonSerializer.Serialize(cart.OrderBy(i => i.ProductId)));
    private IDataProtector CartProtector => HttpContext.RequestServices.GetRequiredService<IDataProtectionProvider>()
        .CreateProtector("ThreeDPrintStore.CartCheckout.v1");
    private void SetCartCookie(string identity) => Response.Cookies.Append(CartCookie, identity, new CookieOptions
    {
        HttpOnly = true, Secure = true, SameSite = SameSiteMode.Lax,
        IsEssential = true, MaxAge = TimeSpan.FromDays(365), Path = "/"
    });
    private bool ValidBinding(string? binding, string cartKey, string? attempt, List<CartItem> cart)
    {
        if (!ValidToken(attempt) || string.IsNullOrEmpty(binding)) return false;
        try { return CartProtector.Unprotect(binding) == cartKey + ":" + attempt + ":" + CartFingerprint(cart); }
        catch (System.Security.Cryptography.CryptographicException) { return false; }
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var identity = Request.Cookies[CartCookie];
        Response.Headers.CacheControl = "no-store";
        if (!ValidToken(identity) || !HttpContext.Session.Keys.Any())
            return View("Bootstrap");
        var cartKey = Hash(identity!);
        var attempt = NewToken();
        var cart = ReadCart();
        ViewData["CheckoutAttempt"] = attempt;
        ViewData["CartBinding"] = CartProtector.Protect(cartKey + ":" + attempt + ":" + CartFingerprint(cart));
        ViewData["CanStartNewOrder"] = await _db.Orders.AsNoTracking()
            .AnyAsync(o => o.ActiveCartKey == cartKey && o.Status != OrderStatus.PendingPayment);
        return View(cart);
    }

    private bool IsInitializationRequest() =>
        Request.Headers["Sec-Fetch-Site"] == "same-origin" &&
        Request.Headers["X-Cart-Initialization"] == "1";

    [HttpPost]
    public IActionResult Initialize()
    {
        Response.Headers.CacheControl = "no-store";
        if (!IsInitializationRequest()) return BadRequest();
        var identity = Request.Cookies[CartCookie];
        if (identity != null && !ValidToken(identity)) return Conflict();
        if (identity == null)
            SetCartCookie(Hash("ThreeDPrintStore.CartIdentity.v1:" + HttpContext.Session.Id));
        if (!HttpContext.Session.Keys.Any())
            HttpContext.Session.SetString("CartIdentityInitialized", "1");
        return NoContent();
    }

    [HttpPost]
    public IActionResult InitializationStatus()
    {
        Response.Headers.CacheControl = "no-store";
        if (!IsInitializationRequest()) return BadRequest();
        if (!ValidToken(Request.Cookies[CartCookie]) || !HttpContext.Session.Keys.Any())
            return Conflict();
        return NoContent();
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> StartNewOrder(string? checkoutAttempt, string? cartBinding)
    {
        var identity = Request.Cookies[CartCookie];
        if (!ValidToken(identity) || !ValidBinding(cartBinding, Hash(identity!), checkoutAttempt, ReadCart()))
            return Conflict("This cart page is out of date. Reload your cart.");
        var key = Hash(identity!);
        var successor = CartProtector.Protect(NewToken());
        await _db.Orders.Where(o => o.ActiveCartKey == key && o.Status != OrderStatus.PendingPayment && o.NextCartIdentity == null)
            .ExecuteUpdateAsync(s => s.SetProperty(o => o.NextCartIdentity, successor));
        var stored = await _db.Orders.AsNoTracking().Where(o => o.ActiveCartKey == key && o.Status != OrderStatus.PendingPayment)
            .Select(o => o.NextCartIdentity).SingleOrDefaultAsync();
        if (stored == null) return Conflict("A new order can only be started after payment is confirmed.");
        try
        {
            var next = CartProtector.Unprotect(stored);
            if (!ValidToken(next)) return Unavailable();
            SetCartCookie(next);
            return RedirectToAction(nameof(Index));
        }
        catch (System.Security.Cryptography.CryptographicException) { return Unavailable(); }
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Checkout(bool includeCacheDrop, string? sponsorName, string? socialHandle, string? sponsorMessage, string? checkoutAttempt = null, string? cartBinding = null)
    {
        if (includeCacheDrop && ((sponsorName?.Length ?? 0) > 100 || (socialHandle?.Length ?? 0) > 100 || (sponsorMessage?.Length ?? 0) > 500))
            return BadRequest("Premium Cache Drop details are too long.");
        var origin = PublicOrigin.Get(_configuration, _environment);
        if (origin == null) return StatusCode(503, "Checkout is unavailable: public origin is not configured.");
        var cart = ReadCart();
        if (cart.Count == 0) return RedirectToAction(nameof(Index));

        var identity = Request.Cookies[CartCookie];
        if (!ValidToken(identity)) return Conflict("Please reload your cart before checking out.");
        var cartKey = Hash(identity!);
        if (!ValidBinding(cartBinding, cartKey, checkoutAttempt, cart))
            return Conflict("This cart page is out of date. Reload your cart.");
        var submissionToken = checkoutAttempt!;

        var subtotal = cart.Sum(i => ProductCatalog.Find(i.ProductId)!.PriceCents * i.Quantity) + (includeCacheDrop ? 500 : 0);
        var shipping = subtotal >= 5000 ? 0 : 895;
        var desired = new Order
        {
            CheckoutToken = NewToken(), CartKey = cartKey, ActiveCartKey = cartKey, CheckoutOrigin = origin,
            SubtotalCents = subtotal, ShippingCents = shipping, TotalCents = subtotal + shipping,
            PremiumCacheDrop = includeCacheDrop,
            SponsorName = includeCacheDrop ? sponsorName?.Trim() : null,
            SocialHandle = includeCacheDrop ? socialHandle?.Trim() : null,
            SponsorMessage = includeCacheDrop ? sponsorMessage?.Trim() : null,
            Items = cart.Select(i => new OrderItem { ProductId = i.ProductId, Name = i.Name, UnitPriceCents = ProductCatalog.Find(i.ProductId)!.PriceCents, Quantity = i.Quantity }).ToList()
        };
        try
        {
            // Retry database uniqueness races by reading the winner, never by creating a second order.
            for (var attempt = 0; attempt < 3; attempt++)
            {
                var submission = await _db.CheckoutSubmissions.AsNoTracking()
                    .FirstOrDefaultAsync(s => s.Token == submissionToken);
                var order = submission != null
                    ? await _db.Orders.Include(o => o.Items).AsNoTracking().SingleAsync(o => o.Id == submission.OrderId)
                    : await _db.Orders.Include(o => o.Items).AsNoTracking().SingleOrDefaultAsync(o => o.ActiveCartKey == cartKey);
                if (order != null)
                {
                    if (order.CartKey != cartKey) return BadRequest("Invalid checkout attempt.");
                    if (order.Status != OrderStatus.PendingPayment || order.ActiveCartKey == null)
                        return Conflict("This checkout has already finished. Reload your cart and explicitly start a new order.");

                    if (order.StripeSessionId != null)
                    {
                        var existing = await GetSessionAsync(order.StripeSessionId);
                        if (existing.Id != order.StripeSessionId) return Unavailable();
                        if (existing.Status == "expired" && existing.PaymentStatus == "unpaid")
                        {
                            await _db.Orders.Where(o => o.Id == order.Id && o.Status == OrderStatus.PendingPayment &&
                                    o.ActiveCartKey == cartKey && o.StripeSessionId == existing.Id)
                                .ExecuteUpdateAsync(s => s.SetProperty(o => o.ActiveCartKey, (string?)null));
                            if (submission != null) return Conflict("This checkout expired. Reload your cart to start a new one.");
                            continue;
                        }
                        if (existing.Status != "open" || existing.PaymentStatus != "unpaid" || string.IsNullOrEmpty(existing.Url))
                            return Unavailable();
                        if (!SameOrder(order, desired))
                            return Conflict("An earlier checkout is still open. Complete it or wait for it to expire before changing your order.");
                        if (submission == null && !await BindSubmission(submissionToken, order.Id)) continue;
                        return Redirect(existing.Url);
                    }

                    if (!SameOrder(order, desired))
                        return Conflict("An earlier checkout is unresolved. Please try again later.");
                    if (submission == null && !await BindSubmission(submissionToken, order.Id)) continue;
                    return await CreateOrRecover(order);
                }

                if (submission != null) return Conflict("This checkout has already finished.");
                desired.CheckoutSubmissions.Add(new CheckoutSubmission { Token = submissionToken });
                _db.Orders.Add(desired);
                try
                {
                    await _db.SaveChangesAsync();
                    return await CreateOrRecover(desired);
                }
                catch (DbUpdateException)
                {
                    _db.ChangeTracker.Clear();
                    desired.CheckoutSubmissions.Clear();
                }
            }
            return Unavailable();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unable to resolve cart checkout.");
            return Unavailable();
        }
    }

    private async Task<bool> BindSubmission(string token, int orderId)
    {
        _db.CheckoutSubmissions.Add(new CheckoutSubmission { Token = token, OrderId = orderId });
        try { await _db.SaveChangesAsync(); return true; }
        catch (DbUpdateException) { _db.ChangeTracker.Clear(); return false; }
    }

    private static bool SameOrder(Order a, Order b) =>
        a.SubtotalCents == b.SubtotalCents && a.ShippingCents == b.ShippingCents &&
        a.PremiumCacheDrop == b.PremiumCacheDrop && a.SponsorName == b.SponsorName &&
        a.SocialHandle == b.SocialHandle && a.SponsorMessage == b.SponsorMessage &&
        a.Items.Count == b.Items.Count && a.Items.All(x => b.Items.Any(y =>
            x.ProductId == y.ProductId && x.Quantity == y.Quantity &&
            x.UnitPriceCents == y.UnitPriceCents && x.Name == y.Name));

    private IActionResult Unavailable() => StatusCode(503, "Checkout is unresolved or temporarily unavailable. Please try again later.");

    private async Task<IActionResult> CreateOrRecover(Order order)
    {
        // Stripe may prune idempotency keys after 24 hours; an unknown outcome is not safe to retry indefinitely.
        if (DateTime.UtcNow - order.CreatedAtUtc >= TimeSpan.FromHours(23)) return Unavailable();
        var lines = order.Items.Select(i => Line(i.Name, i.UnitPriceCents, i.Quantity)).ToList();
        if (order.PremiumCacheDrop) lines.Add(Line("Premium Cache Drop", 500, 1));
        var options = new SessionCreateOptions
        {
            Mode = "payment", LineItems = lines,
            Metadata = new Dictionary<string, string> { ["order_id"] = order.Id.ToString(), ["order_token"] = order.CheckoutToken },
            ShippingAddressCollection = new SessionShippingAddressCollectionOptions { AllowedCountries = new List<string> { "US" } },
            ShippingOptions = new List<SessionShippingOptionOptions> {
                new() { ShippingRateData = new SessionShippingOptionShippingRateDataOptions {
                    Type = "fixed_amount", DisplayName = order.ShippingCents == 0 ? "Free US shipping" : "USPS Shipping",
                    FixedAmount = new SessionShippingOptionShippingRateDataFixedAmountOptions { Amount = order.ShippingCents, Currency = "usd" } } }
            },
            SuccessUrl = order.CheckoutOrigin + "/Cart/Success",
            CancelUrl = order.CheckoutOrigin + "/Cart/Index"
        };
        var session = await CreateSessionAsync(options, order.Id);
        if (string.IsNullOrEmpty(session.Id)) return Unavailable();
        await _db.Orders.Where(o => o.Id == order.Id && o.Status == OrderStatus.PendingPayment &&
                o.ActiveCartKey == order.CartKey && (o.StripeSessionId == null || o.StripeSessionId == session.Id))
            .ExecuteUpdateAsync(s => s.SetProperty(o => o.StripeSessionId, session.Id));
        var current = await _db.Orders.AsNoTracking().SingleAsync(o => o.Id == order.Id);
        if (current.Status != OrderStatus.PendingPayment || current.ActiveCartKey != order.CartKey ||
            current.StripeSessionId != session.Id) return Unavailable();
        if (session.Status == "expired" && session.PaymentStatus == "unpaid")
        {
            await _db.Orders.Where(o => o.Id == order.Id && o.Status == OrderStatus.PendingPayment &&
                    o.ActiveCartKey == order.CartKey && o.StripeSessionId == session.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(o => o.ActiveCartKey, (string?)null));
            return Conflict("This checkout expired. Reload your cart to start a new one.");
        }
        if (session.Status != "open" || session.PaymentStatus != "unpaid" || string.IsNullOrEmpty(session.Url)) return Unavailable();
        return Redirect(session.Url);
    }

    protected virtual Task<Session> CreateSessionAsync(SessionCreateOptions options, int orderId) =>
        new SessionService().CreateAsync(options, new Stripe.RequestOptions { IdempotencyKey = $"cart-order-{orderId}" });

    protected virtual Task<Session> GetSessionAsync(string sessionId) => new SessionService().GetAsync(sessionId);

    private static SessionLineItemOptions Line(string name, long cents, int quantity) => new()
    {
        Quantity = quantity,
        PriceData = new SessionLineItemPriceDataOptions
        {
            Currency = "usd", UnitAmount = cents,
            ProductData = new SessionLineItemPriceDataProductDataOptions { Name = name }
        }
    };

    [HttpGet]
    public IActionResult Success() => View();
}
