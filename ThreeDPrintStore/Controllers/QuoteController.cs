using Microsoft.AspNetCore.Mvc;
using ThreeDPrintStore.Models;

namespace ThreeDPrintStore.Controllers
{
    public class QuoteController : Controller
    {
        private readonly StoreDbContext _context;
        private readonly IWebHostEnvironment _environment;

        // Inject our database and web environment (needed for file uploads)
        public QuoteController(StoreDbContext context, IWebHostEnvironment environment)
        {
            _context = context;
            _environment = environment;
        }

        // 1. GET: /Quote/RequestForm
        [HttpGet]
        public IActionResult RequestForm()
        {
            return View();
        }

        // 2. POST: /Quote/SubmitForm
        [HttpPost]
        public async Task<IActionResult> SubmitForm(QuoteRequest quote, IFormFile? referenceFile)
        {
            if (ModelState.IsValid)
            {
                // Handle image upload if a file was provided
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
                    string filePath = Path.Combine(uploadDir, uniqueFileName);

                    // Save the file to disk
                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await referenceFile.CopyToAsync(fileStream);
                    }

                    // Save the relative web path into our database record
                    quote.ReferenceImagePath = "/uploads/" + uniqueFileName;
                }

                // Force status to Pending initially
                quote.Status = "Pending";

                // Save record to SQLite
                _context.QuoteRequests.Add(quote);
                await _context.SaveChangesAsync();

                // Redirect to a thank you confirmation screen
                return RedirectToAction("Success");
            }

            // If something went wrong or data was missing, reload the form with validation errors
            return View("RequestForm", quote);
        }

        // 3. GET: /Quote/Success
        [HttpGet]
        public IActionResult Success()
        {
            return View();
        }
        // 4. GET: /Quote/Lookup
[HttpGet]
public IActionResult Lookup()
{
    return View();
}

// 5. POST: /Quote/LookupResults
[HttpPost]
public async Task<IActionResult> LookupResults(string customerEmail)
{
    if (string.IsNullOrWhiteSpace(customerEmail))
    {
        return RedirectToAction("Lookup");
    }

    ViewData["SearchedEmail"] = customerEmail.Trim();

    // Query SQLite database for all matching custom entries from this email address
    var matchingQuotes = await _context.QuoteRequests
        .Where(q => q.CustomerEmail.ToLower() == customerEmail.Trim().ToLower())
                                       .OrderByDescending(q => q.Id)
                                       .ToListAsync();

    return View(matchingQuotes);
}



    }
}
