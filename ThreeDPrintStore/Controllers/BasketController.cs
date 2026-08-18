using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using ThreeDPrintStore.Models;

namespace ThreeDPrintStore.Controllers
{
    public class BasketController : Controller
    {
        private readonly StoreDbContext _context;
        private const string BasketSessionKey = "UserShoppingBasket";

        public BasketController(StoreDbContext context)
        {
            _context = context;
        }

        // 1. POST: /Basket/Add
        [HttpPost]
        public IActionResult Add(int productId)
        {
            // Find the item in our SQLite database inventory catalog
            var product = _context.Products.Find(productId);
            if (product == null) return NotFound();

            // Retrieve existing basket dictionary out of session cookie data or start a new one
            var basket = GetBasketFromSession();

            // If item already exists, increase quantity; otherwise add it as a new line entry
            if (basket.ContainsKey(productId))
            {
                basket[productId]++;
            }
            else
            {
                basket[productId] = 1;
            }

            // Save our updated dictionary state back into active session memory strings
            SaveBasketToSession(basket);

            return RedirectToAction("Index");
        }

        // 2. GET: /Basket
        [HttpGet]
        public IActionResult Index()
        {
            var basket = GetBasketFromSession();
            var basketItems = new List<BasketViewModel>();

            foreach (var kvp in basket)
            {
                var product = _context.Products.Find(kvp.Key);
                if (product != null)
                {
                    basketItems.Add(new BasketViewModel
                    {
                        Product = product,
                        Quantity = kvp.Value,
                        TotalLinePrice = product.Price * kvp.Value
                    });
                }
            }

            return View(basketItems);
        }

        // 3. POST: /Basket/Increase
        [HttpPost]
        public IActionResult Increase(int productId)
        {   
            var basket = GetBasketFromSession();
            if (basket.ContainsKey(productId))
            {
                basket[productId]++;
                SaveBasketToSession(basket);
            }
            return RedirectToAction("Index");
        }

            // 4. POST: /Basket/Decrease
            [HttpPost]
        public IActionResult Decrease(int productId)
        {
            var basket = GetBasketFromSession();
            if (basket.ContainsKey(productId))
            {
                if (basket[productId] > 1)
                {
                    basket[productId]--;
                }
                else
                {
                    basket.Remove(productId); // Remove item entirely if quantity hits zero
                }
                SaveBasketToSession(basket);
            }
            return RedirectToAction("Index");
        }

        // 5. POST: /Basket/Remove
        [HttpPost]
        public IActionResult Remove(int productId)
        {
            var basket = GetBasketFromSession();
            if (basket.ContainsKey(productId))
            {
                basket.Remove(productId);
                SaveBasketToSession(basket);
            }
            return RedirectToAction("Index");
        }



        // --- Session Serialization Helper Wrappers ---
        private Dictionary<int, int> GetBasketFromSession()
        {
            var sessionData = HttpContext.Session.GetString(BasketSessionKey);
            return sessionData == null 
                ? new Dictionary<int, int>() 
                : JsonSerializer.Deserialize<Dictionary<int, int>>(sessionData) ?? new Dictionary<int, int>();
        }

        private void SaveBasketToSession(Dictionary<int, int> basket)
        {
            HttpContext.Session.SetString(BasketSessionKey, JsonSerializer.Serialize(basket));
        }
    }

    // Small lightweight data transfer model strictly for view layout updates
    public class BasketViewModel
    {
        public Product Product { get; set; } = null!;
        public int Quantity { get; set; }
        public decimal TotalLinePrice { get; set; }
    }
}
