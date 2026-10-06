using Microsoft.AspNetCore.Mvc; //Allows your class to function as an MVC controller
using System.Text.Json; //Provides JSON serialization and deserialization functionality.
using ThreeDPrintStore.Models; //Gives access to model classes in your project.
using ThreeDPrintStore.Services; //Gives access to service classes (like your shipping service).
using Stripe;
using Microsoft.Extensions.Configuration;

namespace ThreeDPrintStore.Controllers //Defines what namespace this controller belongs too
{
    //class and private fields
    public class CheckoutController : Controller //creates CheckoutController, inheriting from MVC controller.
    {
        private readonly StoreDbContext _context; //My db context, lets me query and save data
        private readonly ShippingService _shippingService; //service responsible for shipping calculations
        private const string BasketSessionKey = "UserShoppingBasket"; //The session key you use to store/retrieve the user's basket.
        private readonly IConfiguration _configuration;

        //responsible for injecting dependencies into the controller
        /**
            -_context = context - saves the injected db context into private field
            -_shippingService - saves the injected shipping service
        **/
        public CheckoutController(StoreDbContext context, ShippingService shippingService, IConfiguration configuration)
        {
            _configuration = configuration;
            _context = context;
            _shippingService = shippingService;
        }

        // 1. GET: /Checkout
        [HttpGet]
        public IActionResult Index() //controller action that returns the checkout view
        {
            var basket = GetBasketFromSession(); //retrieves the user's shopping basket from session storage
            if (!basket.Any()) return RedirectToAction("Index", "Home"); //if the basket is empty, redirect user back to home

            decimal subtotal = CalculateBasketSubtotal(basket); //calculates the subtotal price of all items in the basket

            // Pass a prepared model with item parameters pre-filled
            var orderTemplate = new Order { Subtotal = subtotal }; //creates a new Order object pre-filled with the subtotal
            return View(orderTemplate); //returns the checkout view, populated with the order template
        }

        // 2. POST: /Checkout/PlaceOrder
        [HttpPost] //marks this controller action as responding to HTTP POST requests. Called when user submits form
        public IActionResult PlaceOrder(Order order) //defines asynchronous action method named PlaceOrder that receives an order onject from submitted form
        {
            var basket = GetBasketFromSession(); //retrieves the user's shopping basket from session memory
            if (basket == null || !basket.Any())
            {
                ModelState.AddModelError("", "Your shopping cart session has expired.");
                return View("Index", order);
            }

            order.Subtotal = CalculateBasketSubtotal(basket); //calculates the subtotal cost of all items and assigns it to the order's Subtotal property

            //format and check the incoming city string
            string cityInput = order.City ?? "";
            string postalInput = order.PostalCode ?? "";
            string sanitizedCity = cityInput.Trim().ToLower();
            bool isAlbuquerque = sanitizedCity == "albuquerque" || sanitizedCity == "abq";

            //process according to their selected deliverytype and choice
            if (order.DeliveryType == "PremiumCacheDrop" && isAlbuquerque)
            {
                order.ShippingFee = 0.00m;
                order.CacheUpgradeFee = 10.00m;
            }
            else if (order.DeliveryType == "FreeDelivery" && isAlbuquerque)
            {
                order.ShippingFee = 0.00m;
                order.CacheUpgradeFee = 0.00m;
                order.SponsoredCommunityModelId = null;
            }
            else
            {
                //out of towners or safety fallback
                order.DeliveryType = "Shipping";
                order.CacheUpgradeFee = 0.00m;
                order.SponsoredCommunityModelId = null;
                
                // Execute the Shipping Matrix Engine matching against Albuquerque limits
                order.ShippingFee = _shippingService.CalculateShipping(cityInput, postalInput); //Uses ShippingService to calculate shipping cost based on the order's city and postal code
            }
            
            order.GrandTotal = order.Subtotal + order.ShippingFee + order.CacheUpgradeFee;
            //checks whether the posted form data passed validation rules
            if (ModelState.IsValid)
            {
                //temporarily save the calculated order details inso session
                var orderJson = JsonSerializer.Serialize(order);
                HttpContext.Session.SetString("PendingCheckoutOrder", orderJson);

                //redirect the user straight to the new payment screen
                return RedirectToAction("PaymentSummary");
            }

            return View("Index", order); //If ModelState was not valid earlier, reload the Index view and show validation errors
        }

        // GET: /Checkout/PaymentSummary
        [HttpGet]
        public IActionResult PaymentSummary()
        {
            // 1. Pull the temporary data package out of the user's session memory
            var pendingJson = HttpContext.Session.GetString("PendingCheckoutOrder");

            // 2. Safety Check: If the package is empty (like if someone typed the URL manually), send them back
            if (string.IsNullOrEmpty(pendingJson))
            {
                return RedirectToAction("Index");
            }

            // 3. Unpack the JSON back into an actual "Order" object that C# understands
            var order = JsonSerializer.Deserialize<Order>(pendingJson);
            if (order == null) return RedirectToAction("Index");

            //Initialize Stripe Payment Intent
            StripeConfiguration.ApiKey = _configuration["Stripe:SecretKey"];

            var options = new PaymentIntentCreateOptions
            {
                Amount = (long)(order.GrandTotal * 100),
                Currency = "usd",
                AutomaticPaymentMethods = new PaymentIntentAutomaticPaymentMethodsOptions
                {
                    Enabled = true,
                },
                ReceiptEmail = order.CustomerEmail
            };

            var service = new PaymentIntentService();
            PaymentIntent intent = service.Create(options);

            //send clientsecret & publishahle to view view viewbag for stripes elements JS
            ViewBag.ClientSecret = intent.ClientSecret;
            ViewBag.StripePublishableKey = "pk_live_51TJJNDIyGFfPiJJjCZBndMbpPZhXNgE7OaO7yh2rzLhfWXx6S7CA1DIN4vQsWzymVabwubHG3lQiTyPXQDAeCDV400UGy57KVk";

            // 4. Send that unpacked order data to your new Payment webpage view
            return View(order);
        }

        // POST: /Checkout/ProcessSecurePayment
[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> ProcessSecurePayment(string paymentIntentId)
{
    // 1. Grab that same temporary order package out of session memory again
    var pendingJson = HttpContext.Session.GetString("PendingCheckoutOrder");
    if (string.IsNullOrEmpty(pendingJson))
    {
        return RedirectToAction("Index");
    }

    var order = JsonSerializer.Deserialize<Order>(pendingJson);
    if (order == null)
    {
        ModelState.AddModelError("", "We ran into an issue retrieving your order details. Please try again.");
        return RedirectToAction("Index");
    }

    //verify payment with stripe
    StripeConfiguration.ApiKey = _configuration["Stripe:SecretKey"];
    var intentService = new PaymentIntentService();
    PaymentIntent intent = await intentService.GetAsync(paymentIntentId);

    if (intent.Status != "succeeded")
    {
        ModelState.AddModelError("", "Payment verification failed. Please try again.");
        return View("PaymentSummary", order);
    }

    //Record paid details onto order object
    order.AmountPaid = (decimal)(intent.Amount / 100.0);
    order.PaymentStatus = "Paid";
    order.StripePaymentIntentId = paymentIntentId;

    //save order in SQLite
    _context.Orders.Add(order);

    //Deduct inventory stock
    var basket = GetBasketFromSession();
    if (basket != null)
    {
        foreach (var item in basket)
        {
            // item.Key is the Product ID, item.Value is the Quantity they bought
            var product = await _context.Products.FindAsync(item.Key);
            if (product != null)
            {
                // Math.Max guarantees stock never accidentally drops below zero+
                product.StockQuantity = Math.Max(0, product.StockQuantity - item.Value);
            }
        }
    }

    // 6. Push all changes (the new order record + updated stock levels) to your database at once!
    await _context.SaveChangesAsync();

    // 7. Clean up! Wipe out their temporary checkout sessions and empty their shopping basket
    HttpContext.Session.Remove("PendingCheckoutOrder");
    HttpContext.Session.Remove(BasketSessionKey);

    // 8. Send them straight to your working order success page, passing their new database Order ID
    return RedirectToAction("Confirmation", new { id = order.Id });
}




        // 3. GET: /Checkout/Confirmation/5
        [HttpGet]

        //defines an asynchronous action method called Confirmation.
        //It expects an integer id, which is the Order ID passed in the URL
        public async Task<IActionResult> Confirmation(int id)
        {
            var confirmedOrder = await _context.Orders.FindAsync(id); //looks up the order in the DB by its ID using Entity Framework's asynchronous find
            if (confirmedOrder == null) return NotFound(); // if no matching order exists, return a 404 Not found
            return View(confirmedOrder); //returns the confirmation view, passing the order object into the view so it can be displayed
        }

        // --- Helper Methods ---
        //Defines a private method that returns the user's basket
        //The basket is stored as a dictionary<int,int> where: key = product ID. value = quantity purchased
        private Dictionary<int, int> GetBasketFromSession()
        {
            var sessionData = HttpContext.Session.GetString(BasketSessionKey); //reads a JSON string from session memory under the key BasketSessionKey.

            /**
                If nothing is stored, sessionData will be null
                Otherwise -> deserialize JSON into a dictionary
                The "??" fallback ensures that if deserialization fails, you still return an empty dictionary
            **/
            return sessionData == null ? new Dictionary<int, int>() : JsonSerializer.Deserialize<Dictionary<int, int>>(sessionData) ?? new Dictionary<int, int>();
        }
        //defines a private method that takes the basket dictionary and returns a decimal subtotal 
        private decimal CalculateBasketSubtotal(Dictionary<int, int> basket)
        {
            decimal total = 0.00m; //creates a decimal variable called total initialized to 0
            foreach (var kvp in basket) //Loops through each key/value pair in basket. kvp.Key = product ID, kvp.Value = quantity purchased
            {
                var product = _context.Products.Find(kvp.Key); //loks up the product from the db using the product ID
                if (product != null) total += product.Price * kvp.Value; //if product exists, multiply the product's price by the quantity and add it to the total
            }
            return total; //ends the loop and return the computed subtotal
        }
    }
}
