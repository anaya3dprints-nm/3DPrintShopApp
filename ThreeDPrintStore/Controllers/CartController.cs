using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using ThreeDPrintStore.Models;
using Stripe.Checkout;

namespace ThreeDPrintStore.Controllers;

public class CartController : Controller
{
    [HttpPost]
    public IActionResult Add(
        int productId,
        string name,
        decimal price)
    {
        var cartJson =
            HttpContext.Session.GetString("Cart");

        var cart =
            string.IsNullOrEmpty(cartJson)
            ? new List<CartItem>()
            : JsonSerializer.Deserialize<List<CartItem>>(cartJson)!;

        var existing =
            cart.FirstOrDefault(x =>
                x.ProductId == productId);

        if (existing != null)
        {
            existing.Quantity++;
        }
        else
        {
            cart.Add(new CartItem
            {
                ProductId = productId,
                Name = name,
                Price = price,
                Quantity = 1
            });
        }

        HttpContext.Session.SetString(
            "Cart",
            JsonSerializer.Serialize(cart));

        return Redirect(Request.Headers["Referer"].ToString());
    }

    public IActionResult Index()
    {
        var cartJson =
            HttpContext.Session.GetString("Cart");
         //return Content(cartJson ?? "NULL");

       var cart =
            string.IsNullOrEmpty(cartJson)
          ? new List<CartItem>()
          : JsonSerializer.Deserialize<List<CartItem>>(cartJson)!;

        return View(cart);
    }

    [HttpPost]
    public IActionResult Checkout()
    {
        
var cartJson = HttpContext.Session.GetString("Cart");

    var cart = string.IsNullOrEmpty(cartJson)
        ? new List<CartItem>()
        : JsonSerializer.Deserialize<List<CartItem>>(cartJson)!;

    if (!cart.Any())
    {
        return RedirectToAction("Index");
    }

    // Calculate subtotal
    var subtotal = cart.Sum(i => i.Price * i.Quantity);

    // Build Stripe line items
    var lineItems = cart.Select(item => new SessionLineItemOptions
    {
        PriceData = new SessionLineItemPriceDataOptions
        {
            Currency = "usd",
            UnitAmount = (long)(item.Price * 100),
            ProductData = new SessionLineItemPriceDataProductDataOptions
            {
                Name = item.Name
            }
        },

        Quantity = item.Quantity
    }).ToList();

    // Shipping options
    List<SessionShippingOptionOptions> shippingOptions;

    if (subtotal >= 50)
    {
        shippingOptions = new List<SessionShippingOptionOptions>
        {
            new SessionShippingOptionOptions
            {
                ShippingRateData =
                    new SessionShippingOptionShippingRateDataOptions
                    {
                        Type = "fixed_amount",
                        DisplayName = "Free Shipping",

                        FixedAmount =
                            new SessionShippingOptionShippingRateDataFixedAmountOptions
                            {
                                Amount = 0,
                                Currency = "usd"
                            }
                    }
            }
        };
    }
    else
    {
        shippingOptions = new List<SessionShippingOptionOptions>
        {
            new SessionShippingOptionOptions
            {
                ShippingRateData =
                    new SessionShippingOptionShippingRateDataOptions
                    {
                        Type = "fixed_amount",
                        DisplayName = "Albuquerque / Rio Rancho Delivery",

                        FixedAmount =
                            new SessionShippingOptionShippingRateDataFixedAmountOptions
                            {
                                Amount = 200,
                                Currency = "usd"
                            }
                    }
            },

            new SessionShippingOptionOptions
            {
                ShippingRateData =
                    new SessionShippingOptionShippingRateDataOptions
                    {
                        Type = "fixed_amount",
                        DisplayName = "USPS Shipping",

                        FixedAmount =
                            new SessionShippingOptionShippingRateDataFixedAmountOptions
                            {
                                Amount = 895,
                                Currency = "usd"
                            }
                    }
            }
        };
    }

    var options = new SessionCreateOptions
    {
        Mode = "payment",

        LineItems = lineItems,

        ShippingAddressCollection =
            new SessionShippingAddressCollectionOptions
            {
                AllowedCountries = new List<string> { "US" }
            },

        ShippingOptions = shippingOptions,

        SuccessUrl = Url.Action(
            "Success",
            "Cart",
            null,
            Request.Scheme),

        CancelUrl = Url.Action(
            "Index",
            "Cart",
            null,
            Request.Scheme)
    };

    var service = new SessionService();
    Session session = service.Create(options);

    return Redirect(session.Url);

    }
}   