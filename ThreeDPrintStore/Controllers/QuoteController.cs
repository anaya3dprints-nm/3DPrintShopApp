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
    private readonly IEmailService _emailService;

    public QuoteController(ApplicationDbContext db, IQuoteService quoteService, IEmailService emailService)
    {
        _db = db;
        _quoteService = quoteService;
        _emailService = emailService;
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

[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> SendEmailQuote(int requestId)
{
    var quote = await _db.PrintQuotes.Include(q => q.PrintRequest).FirstOrDefaultAsync(q => q.PrintRequestId == requestId);

    if (quote == null || quote.PrintRequest == null)
    {
        return NotFound();
    }

    //replace this with my existing stripe checkout url genereation logic if I have one
    string stripeCheckoutUrl = Url.Action(
        "Checkout",
        "Cart",
        new { quoteId = quote.Id },
        Request.Scheme
    ) ?? string.Empty;

    await _emailService.SendEmailAsync(quote.PrintRequest, quote, stripeCheckoutUrl);

    quote.PrintRequest.Status = QuoteStatus.QuoteSent;
    await _db.SaveChangesAsync();

    //var emailService = new EmailService();
    //await emailService.SendQuoteEmailAsync(request.CustomerEmail, request.Quote);

    TempData["SuccessMessage"] = $"Quote sent to {quote.PrintRequest.CustomerEmail}!";
    return RedirectToAction(nameof(Details), new { id = requestId });
}

}