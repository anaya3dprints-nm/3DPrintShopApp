using Microsoft.AspNetCore.Mvc; //Allows your class to function as an MVC controller
using System.Text.Json; //Provides JSON serialization and deserialization functionality.
using ThreeDPrintStore.Models; //Gives access to model classes in your project.
using ThreeDPrintStore.Services; //Gives access to service classes (like your shipping service).

namespace ThreeDPrintStore.Controllers //Defines what namespace this controller belongs too
{
    //class and private fields
    public class CheckoutController : Controller //creates CheckoutController, inheriting from MVC controller.
    {
        private readonly StoreDbContext _context; //My db context, lets me query and save data
        private readonly ShippingService _shippingService; //service responsible for shipping calculations
        private const string BasketSessionKey = "UserShoppingBasket"; //The session key you use to store/retrieve the user's basket.

        //responsible for injecting dependencies into the controller
        /**
            -_context = context - saves the injected db context into private field
            -_shippingService - saves the injected shipping service
        **/
        public CheckoutController(StoreDbContext context, ShippingService shippingService)
        {
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
        public async Task<IActionResult> PlaceOrder(Order order) //defines asynchronous action method named PlaceOrder that receives an order onject from submitted form
        {
            var basket = GetBasketFromSession(); //retrieves the user's shopping basket from session memory
            if (!basket.Any()) return RedirectToAction("Index", "Home"); //if basket is empty, redirect the user to the Home page

            order.Subtotal = CalculateBasketSubtotal(basket); //calculates the subtotal cost of all items and assigns it to the order's Subtotal property
            
            // Execute the Shipping Matrix Engine matching against Albuquerque limits
            order.ShippingFee = _shippingService.CalculateShipping(order.City, order.PostalCode); //Uses ShippingService to calculate shipping cost based on the order's city and postal code

            //checks whether the posted form data passed validation rules
            if (ModelState.IsValid)
            {
                // Save Order record cleanly to SQLite
                //adds the order object to the Orders table in the db context (but does not save yet)
                _context.Orders.Add(order);

                // Deduct stock quantities from inventory levels
                foreach (var item in basket) //iterates through each product in the user's basket
                {
                    var product = await _context.Products.FindAsync(item.Key); //looks up the product from the db using its ID (the item.Key)
                    if (product != null) //ensures the product exists
                    {
                        //reduces inventory stock by the quantity purchased
                        //Math.Max prevents negative inventory (sets minimun to 0)
                        product.StockQuantity = Math.Max(0, product.StockQuantity - item.Value);
                    }
                }
                
                await _context.SaveChangesAsync(); //saves all changes made above. The new order and updated product stock quantities. Everything is committed to the db

                // Clear out basket cookies session memory state completely
                HttpContext.Session.Remove(BasketSessionKey); //deletes the basket from session storage so the user's cart is now empty

                return RedirectToAction("Confirmation", new { id = order.Id }); //redirects the user to confirmation page, passing newly created order ID
            }

            return View("Index", order); //If ModelState was not valid earlier, reload the Index view and show validation errors
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
