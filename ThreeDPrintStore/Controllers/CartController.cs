using System.Text.Json;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Mvc;
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

    [HttpGet]
    public IActionResult Index() => View(ReadCart());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Checkout(bool includeCacheDrop, string? sponsorName, string? socialHandle, string? sponsorMessage)
    {
        if (includeCacheDrop && ((sponsorName?.Length ?? 0) > 100 || (socialHandle?.Length ?? 0) > 100 || (sponsorMessage?.Length ?? 0) > 500))
            return BadRequest("Premium Cache Drop details are too long.");
        var origin = PublicOrigin.Get(_configuration, _environment);
        if (origin == null) return StatusCode(503, "Checkout is unavailable: public origin is not configured.");
        var cart = ReadCart();
        if (cart.Count == 0) return RedirectToAction(nameof(Index));

        var subtotal = cart.Sum(i => ProductCatalog.Find(i.ProductId)!.PriceCents * i.Quantity) + (includeCacheDrop ? 500 : 0);
        var shipping = subtotal >= 5000 ? 0 : 895;
        var order = new Order
        {
            CheckoutToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)),
            SubtotalCents = subtotal, ShippingCents = shipping, TotalCents = subtotal + shipping,
            PremiumCacheDrop = includeCacheDrop,
            SponsorName = includeCacheDrop ? sponsorName?.Trim() : null,
            SocialHandle = includeCacheDrop ? socialHandle?.Trim() : null,
            SponsorMessage = includeCacheDrop ? sponsorMessage?.Trim() : null,
            Items = cart.Select(i => new OrderItem { ProductId = i.ProductId, Name = i.Name, UnitPriceCents = ProductCatalog.Find(i.ProductId)!.PriceCents, Quantity = i.Quantity }).ToList()
        };
        _db.Orders.Add(order);
        await _db.SaveChangesAsync();

        var lines = order.Items.Select(i => Line(i.Name, i.UnitPriceCents, i.Quantity)).ToList();
        if (includeCacheDrop) lines.Add(Line("Premium Cache Drop", 500, 1));
        var options = new SessionCreateOptions
        {
            Mode = "payment", LineItems = lines,
            Metadata = new Dictionary<string, string> { ["order_id"] = order.Id.ToString(), ["order_token"] = order.CheckoutToken },
            ShippingAddressCollection = new SessionShippingAddressCollectionOptions { AllowedCountries = new List<string> { "US" } },
            ShippingOptions = new List<SessionShippingOptionOptions> {
                new() { ShippingRateData = new SessionShippingOptionShippingRateDataOptions {
                    Type = "fixed_amount", DisplayName = shipping == 0 ? "Free US shipping" : "USPS Shipping",
                    FixedAmount = new SessionShippingOptionShippingRateDataFixedAmountOptions { Amount = shipping, Currency = "usd" } } }
            },
            SuccessUrl = origin + "/Cart/Success",
            CancelUrl = origin + "/Cart/Index"
        };
        try
        {
            var session = await CreateSessionAsync(options, order.Id);
            if (string.IsNullOrEmpty(session.Id) || string.IsNullOrEmpty(session.Url))
                throw new InvalidOperationException("Stripe did not return a checkout session.");
            order.StripeSessionId = session.Id;
            await _db.SaveChangesAsync();
            return Redirect(session.Url);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unable to create checkout for order {OrderId}", order.Id);
            return StatusCode(503, "Checkout is temporarily unavailable.");
        }
    }

    protected virtual Task<Session> CreateSessionAsync(SessionCreateOptions options, int orderId) =>
        new SessionService().CreateAsync(options, new Stripe.RequestOptions { IdempotencyKey = $"cart-order-{orderId}" });

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
