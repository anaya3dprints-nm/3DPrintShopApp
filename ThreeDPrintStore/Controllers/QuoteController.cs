using Microsoft.AspNetCore.Mvc;
using ThreeDPrintStore.Data;
using ThreeDPrintStore.Models;
using ThreeDPrintStore.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Stripe.Checkout;
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

        //add reference image here
        if (request.ReferenceImage != null && request.ReferenceImage.Length > 0)
        {
            var allowedExtensions = new[] {".jpg", ".jpeg", ".png", ".webp" };
            var extension = Path.GetExtension(request.ReferenceImage.FileName).ToLowerInvariant();

            if (!allowedExtensions.Contains(extension))
            {
                ModelState.AddModelError("ReferenceImage", "Only JPG, PNG, and WebP images are allowed.");
            }
            else if (request.ReferenceImage.Length > 5 * 1024 * 1024)
            {
                ModelState.AddModelError("ReferenceImage", "Image size must be under 5 MB.");
            }
            else
            {
                var refFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "references");
                Directory.CreateDirectory(refFolder);

                var uniqueFileName = $"{Guid.NewGuid()}{extension}";
                var refFilePath = Path.Combine(refFolder, uniqueFileName);

                using (var stream = new FileStream(refFilePath, FileMode.Create))
                {
                    await request.ReferenceImage.CopyToAsync(stream);
                }

                request.ReferenceImagePath = $"/uploads/references/{uniqueFileName}";
            }
        }

        if (!ModelState.IsValid)
        {
            return View(request);
        }

        _db.PrintRequests.Add(request);
        _db.SaveChanges();
        await _emailService.SendSubmissionConfirmationAsync(request);
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
        action: "PayQuote",
        controller: "Quote",
        values: new { quoteId = quote.Id },
        protocol: Request.Scheme
    ) ?? string.Empty;

    await _emailService.SendEmailAsync(quote.PrintRequest, quote, stripeCheckoutUrl);

    quote.PrintRequest.Status = QuoteStatus.QuoteSent;
    await _db.SaveChangesAsync();

    //var emailService = new EmailService();
    //await emailService.SendQuoteEmailAsync(request.CustomerEmail, request.Quote);

    TempData["SuccessMessage"] = $"Quote sent to {quote.PrintRequest.CustomerEmail}!";
    return RedirectToAction(nameof(Details), new { id = requestId });
}

    [HttpGet]
    public async Task<IActionResult> PayQuote(int quoteId)
    {
        var quote = await _db.PrintQuotes
            .Include(q => q.PrintRequest)
            .FirstOrDefaultAsync(q => q.Id == quoteId);

        if (quote == null) return NotFound();

        //prevent double payment
        if (quote.PrintRequest?.Status == QuoteStatus.Paid)
        {
            return RedirectToAction("QuoteAlreadyPaid");
        }

        var domain = $"{Request.Scheme}://{Request.Host}";

        var options = new Stripe.Checkout.SessionCreateOptions
        {
            AllowedPaymentMethodTypes = new List<string> { "card" },
            CustomerEmail = quote.PrintRequest?.CustomerEmail,
            LineItems = new List<Stripe.Checkout.SessionLineItemOptions>
            {
                new Stripe.Checkout.SessionLineItemOptions
                {
                    PriceData = new Stripe.Checkout.SessionLineItemPriceDataOptions
                    {
                        UnitAmount = (long)Math.Round(quote.FinalTotalPrice * 100),
                        Currency = "usd",
                        ProductData = new Stripe.Checkout.SessionLineItemPriceDataProductDataOptions
                        {
                            Name = $"Custom 3D Print Quote #{quote.Id}",
                            Description = $"{quote.PrintRequest?.MaterialType} | {quote.PrintRequest?.RequestedColorsCount} color(s)"
                        }
                    },
                    Quantity = 1
                }
            },
            Mode = "payment",
            Metadata = new Dictionary<string, string>
            {
                { "quote_id", quote.Id.ToString() },
                { "request_id", quote.PrintRequestId.ToString() }
            },
            SuccessUrl = $"{domain}/Quote/PaymentSuccess?session_id={{CHECKOUT_SESSION_ID}}",
            CancelUrl = $"{domain}/Quote/PaymentCancelled?id={quote.Id}"
        };

        var service = new Stripe.Checkout.SessionService();
        var session = await service.CreateAsync(options);

        string stripeCheckoutUrl = session.Url;
        return Redirect(stripeCheckoutUrl); 
    }

    
}
