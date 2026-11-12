using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using ThreeDPrintStore.Models;

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
}