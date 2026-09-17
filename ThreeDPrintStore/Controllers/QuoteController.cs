//These lines (2-4) bring in external libraries your controller depends on
using Microsoft.AspNetCore.Mvc;
using ThreeDPrintStore.Models;
using Microsoft.EntityFrameworkCore;

//Groups this controller class into the Controllers namespace
namespace ThreeDPrintStore.Controllers
{
    //Defines a controller named QuoteController that inherits from ASP.NET MVC’s Controller base class.
    public class QuoteController : Controller
    {

        /**
            private readonly StoreDbContext _context;
            Database context used to query or save data.
            private readonly IWebHostEnvironment _environment;
            Represents the hosting environment, commonly used for file uploads and accessing wwwroot paths.
        **/

        private readonly StoreDbContext _context;
        private readonly IWebHostEnvironment _environment;

        // Inject our database and web environment (needed for file uploads)
        /**
            Constructor that receives dependencies. ASP.NET Core will automatically provide these when the controller is created.
            Inside the constructor:
            _context = context; Stores the database context into the controller’s private field.
            _environment = environment; Stores the hosting environment into its private field.
        **/
        public QuoteController(StoreDbContext context, IWebHostEnvironment environment)
        {
            _context = context;
            _environment = environment;
        }

        // 1. GET: /Quote/RequestForm
        [HttpGet]
        //Defines a standard (non‑async) action that returns a view.
        public IActionResult RequestForm()
        {
            return View(); //Returns the default view associated with RequestForm. No model is sent — this simply loads the form page.
        }

        // 2. POST: /Quote/SubmitForm
        [HttpPost]
        /**
            Defines an asynchronous controller action named SubmitForm.
            It receives two things from the submitted form:
                quote → a QuoteRequest model with form data
                referenceFile → an optional uploaded file (image, PDF, etc.)
        **/
        public async Task<IActionResult> SubmitForm(QuoteRequest quote, IFormFile? referenceFile)
        {
            if (ModelState.IsValid) //begins the method body. Checkswhether validation rules on quote were satidfies. If not valid, skip to the bottom and return the form again
            {
                // Handle image upload if a file was provided
                /**
                    Checks whether the user actually uploaded a file:
                        referenceFile != null → file exists
                        referenceFile.Length > 0 → file contains data
                    Only then will this block run.
                **/
                if (referenceFile != null && referenceFile.Length > 0)
                {
                    // Create an 'uploads' directory inside wwwroot if it doesn't exist
                    string uploadDir = Path.Combine(_environment.WebRootPath, "uploads");
                    if (!Directory.Exists(uploadDir))
                    {
                        Directory.CreateDirectory(uploadDir);
                    }

                    // Generate a completely unique filename to avoid overwrites
                    string uniqueFileName = Guid.NewGuid().ToString() + "_" + Path.GetFileName(referenceFile.FileName);
                    string filePath = Path.Combine(uploadDir, uniqueFileName); //builds the full path where the file will be saved on disk

                    // opens a new file stream (creates a new file)
                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        //writes the uploaded file into that new file asynchronously
                        //the using block ensures the stream is closed automatically afterwards
                        await referenceFile.CopyToAsync(fileStream);
                    }

                    // Save the relative web path into our database record, QuoteRequest
                    quote.ReferenceImagePath = "/uploads/" + uniqueFileName;
                }

                // Force status to Pending initially
                quote.Status = "Pending";

                // Save record to SQLite
                _context.QuoteRequests.Add(quote); //adds the quote request entry to the EF Core db context
                await _context.SaveChangesAsync(); //commits the new quote record into the SQLite db

                // Redirect to a thank you confirmation screen
                return RedirectToAction("Success");
            }

            // If something went wrong or data was missing, reload the form with validation errors
            /**
                If validation failed:
                    reload the RequestForm view
                    pass back the quote object
                    validation messages will display next to form fields
            **/
            return View("RequestForm", quote);
        }

        // 3. GET: /Quote/Success
        [HttpGet]
        //declares an action method named Success
        //It returns a standard IActionResult (a view)
        public IActionResult Success()
        {
            return View(); //returns the default view named "Success"
        }
        // 4. GET: /Quote/Lookup
[HttpGet]
//declares an action named Lookup, which shows a form where the user can enter their email to look up quotes
public IActionResult Lookup()
{
    return View(); //returns the default "Lookup" view from the form
}

// 5. POST: /Quote/LookupResults
[HttpPost]//Specifies that this action responds to POST requests (form submissions)

//Declares an asynchronous action named LookupResults
//It receievs one parameter: customerEmail, the value the user typed into the lookup form
public async Task<IActionResult> LookupResults(string customerEmail)
{
    if (string.IsNullOrWhiteSpace(customerEmail)) //checks if the email string is empty, null, just whitespace; if so, user didn't enter a valid email.
    {
        return RedirectToAction("Lookup"); //if email is invalid, redirec the user back to the lookup form
    }

    ViewData["SearchedEmail"] = customerEmail.Trim(); //saves the searched email (trimmed of extra spaces) into ViewData

    // Begins a LINQ query against the QuoteRequest table in SQLite db
    //filters quotes so only records with matching customer email are returned
    //both sides are converted to lowercase to make the comparison case-insensitive
    //sorts results so most recent quote appears first
    //executes the query asynchronously and converts results into a list
    var matchingQuotes = await _context.QuoteRequests
        .Where(q => q.CustomerEmail.ToLower() == customerEmail.Trim().ToLower())
                                       .OrderByDescending(q => q.Id)
                                       .ToListAsync();
    //returns a view whose data model is the list of matching quotes. 
    return View(matchingQuotes);
}



    }
}
