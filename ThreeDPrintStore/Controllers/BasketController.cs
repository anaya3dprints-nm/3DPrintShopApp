using Microsoft.AspNetCore.Mvc; //lets this function as an MVC controller
using System.Text.Json; //enables working with JSON (used for storing basket data in session).
using ThreeDPrintStore.Models; //allows the controller to use your model classes (like Product).

namespace ThreeDPrintStore.Controllers //Groups this controller inside the Controllers folder of my project
{

    //Defines the BasketController and inherits from Controller, giving it access to MVC methods such as View(), RedirectToACtion(), and session access
    public class BasketController : Controller
    {    
        //_context - provides access to the database (Products table, etc.)
        //BasketSessionKey - the name used to store/retrieve the basket in session as JSON.
        private readonly StoreDbContext _context;
        private const string BasketSessionKey = "UserShoppingBasket";

        //ASP.NET core injects the database context automatically.
        //The constructor saves it into _context so other methods he can use it.
        public BasketController(StoreDbContext context)
        {
            _context = context;
        }

        //MArks this action as a POST request
        [HttpPost]

        //Defines the method Add, which takes a product ID from the request.
        public IActionResult Add(int productId)
        {
            //Queries the Products table for a product with the given ID
            // If not found, immediately returns a 404 response
            var product = _context.Products.Find(productId);
            if (product == null) return NotFound();

            //Loads the basket from the current session
            var basket = GetBasketFromSession();

            // If product already exists, increase its quantity
            // If not, add product with quantity 1.
            if (basket.ContainsKey(productId))
            {
                basket[productId]++;
            }
            else
            {
                basket[productId] = 1;
            }

            // Stores the basket back into the session, usually by serializing it as JSON
            SaveBasketToSession(basket);

            //Redirects the user back to the page named "Index"
            return RedirectToAction("Index");
        }

        // Declares that this action responds to an HTTP GET request to /Basket
        [HttpGet]

        //Defines an action named Index. It returns an IActionResult, meaning it will render a view, redirect, or return some HTTP response.
        public IActionResult Index()
        {
            /**
               Calls a helper function which retrieves the user's basket from the session. Basket likely a dictionary 
            **/
            var basket = GetBasketFromSession();

            //creates an empty list of view-model objects that will be passed to the Razor view.
            var basketItems = new List<BasketViewModel>();

            foreach (var kvp in basket) //Loops through each entry in the basket dictionary. kvp stands for key-value pair.
            {
                var product = _context.Products.Find(kvp.Key); //Looks up the actual product in the database using the ID
                if (product != null) //Ensures the product exists.
                {

                    /**
                        Creates a new BasketViewModel object containing:
                        -Product: the db product record
                        -Quantity: how many of that item the user has
                        -TotalLinePrice - computed as (product price * quantity)
                    basketItems.Add(new BasketViewModel
                    {
                        Product = product,
                        Quantity = kvp.Value,
                        TotalLinePrice = product.Price * kvp.Value
                    });
                }
            }
            //send the completed list to the Razor view so the UI can dispaly the shopping basket.
            return View(basketItems);
        }

       
        [HttpPost] //Marks this action as an HTTP POST endpoint at /Basket/Increase

        //Defines an action method named Increase
        //It accepts a product ID and returns an IActionResult
        public IActionResult Increase(int productId)
        {   
            var basket = GetBasketFromSession(); //Gets the current shopping basket from the session. Basket likely a dictionary
            if (basket.ContainsKey(productId)) //Checks if the product already exists in the basket
            {
                basket[productId]++; //If it exists, increase the quantity by 1
                SaveBasketToSession(basket); //saves the modified basket back into the season
            }
            return RedirectToAction("Index"); //redirects the user back to the basket page, causing it to refresh and show updated quantities.
        }

            //Marks this action as an HTTP POST endpoint at /Basket/Decrease
            [HttpPost]
        public IActionResult Decrease(int productId) //Defines the Decrease method. It accepts a product ID and returns an action result
        {
            var basket = GetBasketFromSession(); //Loads the current basket from session
            if (basket.ContainsKey(productId)) //checks whether that product is in the basket
            {
                if (basket[productId] > 1) //if the current quantity is more than 1, then subtract one.
                {
                    basket[productId]--; //reduces quantity by one
                }
                else
                {
                    basket.Remove(productId); // Remove item entirely if quantity hits zero
                }
                SaveBasketToSession(basket); //Saves the updated basket back into session
            }
            return RedirectToAction("Index"); //redirects back to the basket page so the UI refreshes
        }

        //Marks this action as an HTTP POST endpoint at /Basket/Remove
        [HttpPost]

        //Defines an action named Remove. Receives a productId and returns as MVC ActionResult
        public IActionResult Remove(int productId)
        {
            var basket = GetBasketFromSession(); //Loads the basket dictionary from the session
            if (basket.ContainsKey(productId)) //Checks whether the product exists in the basket
            {
                basket.Remove(productId); //if it exists, remove the product enrty entirely from the dictionary.
                SaveBasketToSession(basket); //SaveBasketToSession(basket);Show more lines. Save the updated basket back to the session, serialized as JSON.
            }
            return RedirectToAction("Index"); //redirects back to the basket page so the UI updates.
        }



        // --- Session Serialization Helper Wrappers ---
        private Dictionary<int, int> GetBasketFromSession() //Defines a private method that returns a dictionary of product IDs and quantities.
        {

            /**
                Looks up the seeion entry for "UserSHoppingBasket"
                    If it exists -> sessionData contains JSON string
                    If nog -> sessionData null
            **/
            var sessionData = HttpContext.Session.GetString(BasketSessionKey);
            
            return sessionData == null //checks whether the session string exists
                ? new Dictionary<int, int>()  //if it does not exist, return an empty dictionary, means user starts with an empty basket
                : JsonSerializer.Deserialize<Dictionary<int, int>>(sessionData) ?? new Dictionary<int, int>(); //If it does exist, deserialize the JSON back into a dictionary
        }

        private void SaveBasketToSession(Dictionary<int, int> basket) //Defines a private helper method that accepts the basket dictionary.
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
