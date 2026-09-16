//namespaces (libraies)
using Microsoft.AspNetCore.Mvc; //enables the use of MVC features like controllers and actions
using Microsoft.EntityFrameworkCore;//enables the use of Entity Framework Core features like database context and async queries
using ThreeDPrintStore.Models;//allows access to the models defined in the ThreeDPrintStore.Models namespace, such as Product and StoreDbContext

//This groups the HomeController class logically inside the Controllers foler of project
namespace ThreeDPrintStore.Controllers
{
    public class HomeController : Controller //Defines a new controller named HomeController that inherits from the Controller class, which gives it MVC abilities (returning views, routing, etc.).
    {
        //creates a variable named _context that holds your database context
        //readonly means it can only be set once - in the constructor
        //You'll use _context to talk to the database (Products, ect.)
        private readonly StoreDbContext _context; 

        //ASP.NET Core's dependency injection system automatically provides a StoreDbCOntext when the controller is created.
        //This allows the controller to interact with the database without needing to manually create an instance of StoreDbContext.
        public HomeController(StoreDbContext context)
        {
            _context = context;
        }

        //This method runs when the user goes to /Home/Home (or /Home if routed differently).
        //IActionResult means it returns an MVC action (like a view)
        //return View("Home") tells ASP.NET to return the Home.cshtml view.
        //Action does not fetch data - it only return the Home.cshtml view.
        public IActionResult Home()
        {
            //load the homepage view
            return View("Home");
        }

        //async Task<IActionResult> - this action runs asynchronously because its doing a database call.
        //_context.Products - refers to the Products table in your database.
        //.ToListAsync() - fetches all the product rows asynchronously.
        //activeInventory - variable that stores all the products.
        //return View("Index", activeInventory) - sends the data to your Index.cshtml view.
        //SO the Index page is my catalog, and it displays the list of products coming from the database.
        public async Task<IActionResult> Index()
        {
            var activeInventory = await _context.Products.ToListAsync();
            return View("Index", activeInventory); //Index.cshtml = Catalog page
        }
    }
}
