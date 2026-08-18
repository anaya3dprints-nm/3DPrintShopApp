using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using ThreeDPrintStore.Models;
using ThreeDPrintStore.Services;

namespace ThreeDPrintStore.Controllers
{
    public class CheckoutController : Controller
    {
        private readonly StoreDbContext _context;
        private readonly ShippingService _shippingService;
        private const string BasketSessionKey = "UserShoppingBasket";

        public CheckoutController(StoreDbContext context, ShippingService shippingService)
        {
            _context = context;
            _shippingService = shippingService;
        }

        // 1. GET: /Checkout
        [HttpGet]
        public IActionResult Index()
        {
            var basket = GetBasketFromSession();
            if (!basket.Any()) return RedirectToAction("Index", "Home");

            decimal subtotal = CalculateBasketSubtotal(basket);

            // Pass a prepared model with item parameters pre-filled
            var orderTemplate = new Order { Subtotal = subtotal };
            return View(orderTemplate);
        }

        // 2. POST: /Checkout/PlaceOrder
        [HttpPost]
        public async Task<IActionResult> PlaceOrder(Order order)
        {
            var basket = GetBasketFromSession();
            if (!basket.Any()) return RedirectToAction("Index", "Home");

            order.Subtotal = CalculateBasketSubtotal(basket);
            
            // Execute the Shipping Matrix Engine matching against Albuquerque limits
            order.ShippingFee = _shippingService.CalculateShipping(order.City, order.PostalCode);

            if (ModelState.IsValid)
            {
                // Save Order record cleanly to SQLite
                _context.Orders.Add(order);

                // Deduct stock quantities from inventory levels
                foreach (var item in basket)
                {
                    var product = await _context.Products.FindAsync(item.Key);
                    if (product != null)
                    {
                        product.StockQuantity = Math.Max(0, product.StockQuantity - item.Value);
                    }
                }

                await _context.SaveChangesAsync();

                // Clear out basket cookies session memory state completely
                HttpContext.Session.Remove(BasketSessionKey);

                return RedirectToAction("Confirmation", new { id = order.Id });
            }

            return View("Index", order);
        }

        // 3. GET: /Checkout/Confirmation/5
        [HttpGet]
        public async Task<IActionResult> Confirmation(int id)
        {
            var confirmedOrder = await _context.Orders.FindAsync(id);
            if (confirmedOrder == null) return NotFound();
            return View(confirmedOrder);
        }

        // --- Helper Methods ---
        private Dictionary<int, int> GetBasketFromSession()
        {
            var sessionData = HttpContext.Session.GetString(BasketSessionKey);
            return sessionData == null ? new Dictionary<int, int>() : JsonSerializer.Deserialize<Dictionary<int, int>>(sessionData) ?? new Dictionary<int, int>();
        }

        private decimal CalculateBasketSubtotal(Dictionary<int, int> basket)
        {
            decimal total = 0.00m;
            foreach (var kvp in basket)
            {
                var product = _context.Products.Find(kvp.Key);
                if (product != null) total += product.Price * kvp.Value;
            }
            return total;
        }
    }
}
