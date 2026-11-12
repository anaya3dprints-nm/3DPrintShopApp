using Microsoft.AspNetCore.Mvc;
using ThreeDPrintStore.Data;
using ThreeDPrintStore.Models;
using ThreeDPrintStore.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System.IO;

public class QuoteController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IQuoteService _quoteService;

    public QuoteController(ApplicationDbContext db, IQuoteService quoteService)
    {
        _db = db;
        _quoteService = quoteService;
    }

    public IActionResult AdminRequests()
    {
        var requests = _db.PrintRequests
                      .OrderByDescending(r => r.SubmittedAt)
                      .ToList();

        return View(requests);
    }


    [HttpGet]
    public IActionResult RequestForm()
    {
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> RequestForm(PrintRequest request, IFormFile modelFile)
    {
        request.MaterialType = "PLA";
        if (modelFile != null)
        {
            var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
            
            Directory.CreateDirectory(uploadsFolder); 
            
            var fileName = Guid.NewGuid() + Path.GetExtension(modelFile.FileName);

            var filePath = Path.Combine(uploadsFolder, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await modelFile.CopyToAsync(stream);
            }

            request.FilePath = fileName;
        }

        if (!ModelState.IsValid)
        {
            return View(request);
        }

        _db.PrintRequests.Add(request);
        _db.SaveChanges();

        return RedirectToAction(nameof(ThankYou));
    }

    public IActionResult ThankYou()
    {
        return View();
    }


public async Task<IActionResult> Details(int id)
{
   var request = await _db.PrintRequests.Include(r => r.Quote).FirstOrDefaultAsync(r => r.Id == id);

   if (request == null)
    {
         return NotFound();
    }
    return View(request);
}

[HttpPost]
[ValidateAntiForgeryToken]
public IActionResult GenerateQuote(QuoteCalculationInput input)
{
    if (!ModelState.IsValid)
    {
        return RedirectToAction(nameof(Details), new { id = input.PrintRequestId });
    }

    
    var quote = _quoteService.CalculateAndSaveQuote(input);

    return RedirectToAction(nameof(Details), new {id = input.PrintRequestId});
}
}