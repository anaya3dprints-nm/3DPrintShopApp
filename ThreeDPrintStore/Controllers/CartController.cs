using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using ThreeDPrintStore.Models;
using Stripe.Checkout;

namespace ThreeDPrintStore.Controllers;

public class CartController : Controller
{
    [HttpPost]
    public IActionResult AddAjax(
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

        //return Redirect(Request.Headers["Referer"].ToString());
        return Json(new
        {
            success = true,
            cartCount = cart.Sum(x => x.Quantity)
        });
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
    public IActionResult Checkout(bool includeCacheDrop, string? sponsorName, string? socialHandle, string? sponsorMessage)
    {
    if (includeCacheDrop &&
        ((sponsorName?.Length ?? 0) > 100 ||
         (socialHandle?.Length ?? 0) > 100 ||
         (sponsorMessage?.Length ?? 0) > 500))
    {
        return BadRequest("Premium Cache Drop details are too long.");
    }
        
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

    if (includeCacheDrop)
    {
        const decimal cacheDropPrice = 5m;
        subtotal += cacheDropPrice;
        lineItems.Add(new SessionLineItemOptions
        {
            PriceData = new SessionLineItemPriceDataOptions
            {
                Currency = "usd",
                UnitAmount = (long)(cacheDropPrice * 100),
                ProductData = new SessionLineItemPriceDataProductDataOptions
                {
                    Name = "Premium Cache Drop"
                }
            },
            Quantity = 1
        });
    }

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

    if (includeCacheDrop)
    {
        options.Metadata = new Dictionary<string, string>
        {
            ["premium_cache_drop"] = "true"
        };
        if (!string.IsNullOrWhiteSpace(sponsorName))
            options.Metadata["sponsor_name"] = sponsorName.Trim();
        if (!string.IsNullOrWhiteSpace(socialHandle))
            options.Metadata["social_handle"] = socialHandle.Trim();
        if (!string.IsNullOrWhiteSpace(sponsorMessage))
            options.Metadata["sponsor_message"] = sponsorMessage.Trim();
    }

    var service = new SessionService();
    Session session = service.Create(options);

    return Redirect(session.Url);

    }
}   