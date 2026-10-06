using Microsoft.AspNetCore.Mvc;
using Stripe;
using Stripe.Checkout;
using ThreeDPrintStore.Models;

namespace ThreeDPrintStore.Controllers
{
    public class CheckoutController : Controller
    {
        private readonly StoreDbContext _context;
        private readonly IConfiguration _configuration;
        private const string BasketSessionKey = "UserShoppingBasket";

        public CheckoutController(StoreDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        // 1. GET: /Checkout
        [HttpGet]
        public IActionResult Index()
        {
            var basket = GetBasketFromSession();
            if (!basket.Any()) return RedirectToAction("Index", "Home");

            return View(basket);
        }

        // 2. POST: /Checkout/CreateCheckoutSession
[HttpPost]
public IActionResult CreateCheckoutSession()
{
    var basket = GetBasketFromSession();
    if (!basket.Any()) return RedirectToAction("Index", "Home");

    StripeConfiguration.ApiKey = _configuration["Stripe:SecretKey"] ?? "sk_test_YOUR_KEY";

    var lineItems = new List<SessionLineItemOptions>();

    foreach (var item in basket)
    {
        var product = _context.Products.Find(item.Key);
        if (product != null)
        {
            lineItems.Add(new SessionLineItemOptions
            {
                PriceData = new SessionLineItemPriceDataOptions
                {
                    UnitAmount = (long)(product.Price * 100),
                    Currency = "usd",
                    ProductData = new SessionLineItemPriceDataProductDataOptions
                    {
                        Name = product.Name ?? "Product",
                        Description = product.Description
                    },
                },
                Quantity = item.Value,
            });
        }
    }

    var domain = $"{Request.Scheme}://{Request.Host}";

    var options = new SessionCreateOptions
    {
        // PaymentMethodTypes removed - Stripe handles card payments automatically!
        LineItems = lineItems,
        Mode = "payment",
        ShippingAddressCollection = new SessionShippingAddressCollectionOptions
        {
            AllowedCountries = new List<string> { "US" },
        },
        ShippingOptions = new List<SessionShippingOptionOptions>
        {
            new SessionShippingOptionOptions
            {
                ShippingRateData = new SessionShippingOptionShippingRateDataOptions
                {
                    Type = "fixed_amount",
                    FixedAmount = new SessionShippingOptionShippingRateDataFixedAmountOptions { Amount = 500, Currency = "usd" },
                    DisplayName = "Standard Mail Shipping",
                    DeliveryEstimate = new SessionShippingOptionShippingRateDataDeliveryEstimateOptions
                    {
                        Minimum = new SessionShippingOptionShippingRateDataDeliveryEstimateMinimumOptions { Unit = "business_day", Value = 3 },
                        Maximum = new SessionShippingOptionShippingRateDataDeliveryEstimateMaximumOptions { Unit = "business_day", Value = 5 },
                    }
                }
            },
            new SessionShippingOptionOptions
            {
                ShippingRateData = new SessionShippingOptionShippingRateDataOptions
                {
                    Type = "fixed_amount",
                    FixedAmount = new SessionShippingOptionShippingRateDataFixedAmountOptions { Amount = 0, Currency = "usd" },
                    DisplayName = "Free Local Delivery (ABQ Exclusive)",
                }
            },
            new SessionShippingOptionOptions
            {
                ShippingRateData = new SessionShippingOptionShippingRateDataOptions
                {
                    Type = "fixed_amount",
                    FixedAmount = new SessionShippingOptionShippingRateDataFixedAmountOptions { Amount = 1000, Currency = "usd" },
                    DisplayName = "Premium Cache Drop Scavenger Hunt",
                }
            }     
        },
    ExtraParams = new Dictionary<string, object>
    {
        {
            "custom_fields", new[]
            {
                new Dictionary<string, object>
                {
                    { "key", "sponsored_community_model" },
                    {
                        "label", new Dictionary<string, object>
                        {
                            { "type", "custom" },
                            { "custom", "Sponsored Community Model (Optional)" }
                        }
                    },
                    { "type", "text" },
                    { "optional", true }
                },
                new Dictionary<string, object>
                {
                    { "key", "delivery_instructions" },
                    {
                        "label", new Dictionary<string, object>
                        {
                            { "type", "custom" },
                            { "custom", "Delivery or Cache Drop Instructions" }
                        }
                    },
                    { "type", "text" },
                    { "optional", true }
                }
            }
        }
        
        },

        CustomText = new SessionCustomTextOptions
        {
            ShippingAddress = new SessionCustomTextShippingAddressOptions
            {
                Message = "Free local delivery is available exclusively for Albuquerque (ABQ) addresses."
            },
            Submit = new SessionCustomTextSubmitOptions
            {
                Message = "Thank you for supporting 3D printing in the local community!"
            }
        },


        SuccessUrl = $"{domain}/Checkout/Confirmation?session_id={{CHECKOUT_SESSION_ID}}",
        CancelUrl = $"{domain}/Checkout/Index",
    };

    var service = new SessionService();
    Session session = service.Create(options);

    return Redirect(session.Url);
}

// 3. GET: /Checkout/Confirmation?session_id=cs_test_...
[HttpGet]
public async Task<IActionResult> Confirmation(string session_id)
{
    if (string.IsNullOrEmpty(session_id)) return RedirectToAction("Index", "Home");

    StripeConfiguration.ApiKey = _configuration["Stripe:SecretKey"] ?? "sk_test_YOUR_KEY";
    var service = new SessionService();
    Session session = await service.GetAsync(session_id);

    if (session.PaymentStatus == "paid")
    {
        // Extract the user's typed response from Stripe's custom text field
        string sponsoredModelText = session.CustomFields
            ?.FirstOrDefault(f => f.Key == "sponsored_community_model")
            ?.Text?.Value ?? "";

        string deliveryInstructions = session.CustomFields
            ?.FirstOrDefault(f => f.Key == "delivery_instructions")
            ?.Text?.Value ?? "";

        // Parse integer ID if numeric, otherwise store null
        int? communityModelId = int.TryParse(sponsoredModelText, out int parsedId) ? parsedId : null;


        var order = new Order
        {
            EmailAddress = session.CustomerDetails?.Email ?? session.CustomerEmail ?? "",
            AmountPaid = (decimal)((session.AmountTotal ?? 0) / 100.0),
            PaymentStatus = "Paid",
            StripePaymentIntentId = session.PaymentIntentId ?? session.Id,
            City = session.CustomerDetails?.Address?.City ?? "",
            PostalCode = session.CustomerDetails?.Address?.PostalCode ?? "",
            SponsoredCommunityModelId = communityModelId
        };

        _context.Orders.Add(order);

        var basket = GetBasketFromSession();
        foreach (var item in basket)
        {
            var product = await _context.Products.FindAsync(item.Key);
            if (product != null)
            {
                product.StockQuantity = Math.Max(0, product.StockQuantity - item.Value);
            }
        }

        await _context.SaveChangesAsync();

        HttpContext.Session.Remove(BasketSessionKey);

        return View(order);
    }

    return RedirectToAction("Index");
}

        
        

        private Dictionary<int, int> GetBasketFromSession()
        {
            var sessionData = HttpContext.Session.GetString(BasketSessionKey);
            return sessionData == null 
                ? new Dictionary<int, int>() 
                : System.Text.Json.JsonSerializer.Deserialize<Dictionary<int, int>>(sessionData) ?? new Dictionary<int, int>();
        }
    }
}