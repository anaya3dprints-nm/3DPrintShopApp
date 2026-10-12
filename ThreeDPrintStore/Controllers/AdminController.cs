using Microsoft.AspNetCore.Mvc; //provides MVC features like controllers, routing, IActionResult, and View().
using Microsoft.EntityFrameworkCore; //Enables DB access using Entity Framework Core.
using ThreeDPrintStore.Models; //Gives access to your model classes (like Product, Order, etc.)
using ThreeDPrintStore.Services; //Allows the controller to use custom services I createdm such as PricingService and ShippingService.

//This groups this controller as part of the controllers folder/namespace within your project
namespace ThreeDPrintStore.Controllers
{
    //Defines a controller named AdminController. It inherits from Controller, so it automatically gains MVC abilities like returning views and using midel binding.
    public class AdminController : Controller
    {
        private readonly StoreDbContext _context; //allows reading/writing data in the DB
        private readonly PricingService _pricingService; //a custom service that calculates pricing, fees, etc.
        private readonly ShippingService _shippingService; //a custom service that handles shipping calculations

        //readonly means they can only be assigned once (normally in the constructor)

        // Inject database context along with our pricing and shipping matrices
        //The controller strores those in its private fields so that the rest of the controller can use them
        public AdminController(StoreDbContext context, PricingService pricingService, ShippingService shippingService)
        {
            _context = context;
            _pricingService = pricingService;
            _shippingService = shippingService;
        }

        //This marks the method as handling HTTP GET requests. Meaning: when someone visits /Admin/Dashboard in the browser, this method runs. 
        [HttpGet]

        //public - accessible to the framework; can be called externally.
        //async - this method will run asynchronously
        //Task<IActionResult> - return a MVC action result (a view, redirect, etc.) asynchronously
        //Dashboard() - method name
        public async Task<IActionResult> Dashboard()
        {
            //var quotes = creates a variable quotes to store the results
            //await _context.QuoteRequests = access the QuoteRequests table in mt DB using Entity Framework Core.
            //.OrderByDescending(q => q.Id) = Sorts the quote requests by their ID, highest first (newest first)
            //.ToListAsync() = Executes the query asynchronously and returns a list. 
            var quotes = await _context.QuoteRequests
                                       .OrderByDescending(q => q.Id)
                                       .ToListAsync();
            return View(quotes); //retutn the dashboard view and passes the list of quotes into it so the UI can display them
        }

        //This method handles HTTP POST requests - typically a form submission
        [HttpPost]

        //public - accessible externally
        //async - users asynchronous operations
        //Task<IActionResult> - returns an MVC action result
        //CalculateQuote(...) - method name
        /** 
            parameters:
            quoteId - identifies which quote request to update
            grams-Used - how much filament is used in grams
            printHours - estimated printed times
        **/
        //These likely come from a form submitted in the admin UI
        public async Task<IActionResult> CalculateQuote(int quoteId, double gramsUsed, double printHours)
        {
            //var quoteRequest = hold the result
            //await _context.QuoteRequests.FindAsync(quoteId) - looks up the quote with matching quoteId in the database
            var quoteRequest = await _context.QuoteRequests.FindAsync(quoteId);

            //If no quote exists with the ID -> return a 404 Not Found response
            if (quoteRequest == null)
            {
                return NotFound();
            }

            /**
                decimal printBasePrice - stores the calculated base price
                _pricingService.CalculateCustomPrintPrice(...)
                    Calls your pricing service to compute price using:
                    -grams of filament used
                    -number of printing hours
            **/
            
            decimal printBasePrice = _pricingService.CalculateCustomPrintPrice(gramsUsed, printHours);
            
            /**
                decimal shippingCost - store shipping cost
                _shippingService.CalculateShipping(...)
                    Calls your shipping service to compute cost based on:
                    -the destination city
                    -the postal code
            **/
            decimal shippingCost = _shippingService.CalculateShipping(quoteRequest.ShippingCity, quoteRequest.PostalCode);

            // Save metrics back into our database entity structure

            /**
                -quoteRequest.CalculatedPrice = printBasePrice; Save the pricing result inside the request object.
                -quoteRequest.ShippingFee = shippingCost; Store the shipping fee.
                -quoteRequest.Status = "Calculated"; Updates the status so the client-facing UI knows the quote is ready.
            **/
            quoteRequest.CalculatedPrice = printBasePrice;
            quoteRequest.ShippingFee = shippingCost;
            quoteRequest.Status = "Calculated"; // Ready for client delivery pipeline

            _context.Entry(quoteRequest).State = EntityState.Modified; //Tells Entity Framework Core that the quoteRequest object has been changed and needs updating in database.
            await _context.SaveChangesAsync(); //Writes updates (pricing, shipping fee, status) to the database asynchronously

            // After saving the quote, the admin user is redirected back to the dashboard
            return RedirectToAction("Dashboard");
        }
    }
}
