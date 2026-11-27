using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ThreeDPrintStore.Data;
using ThreeDPrintStore.Models;
using ThreeDPrintStore.Services;

public class QuoteController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IEmailService _emailService;
    private readonly IWebHostEnvironment _environment;
    private readonly IConfiguration _configuration;
    private readonly ILogger<QuoteController> _logger;

    public QuoteController(ApplicationDbContext db, IEmailService emailService, IWebHostEnvironment environment,
        IConfiguration configuration, ILogger<QuoteController> logger)
    {
        _db = db;
        _emailService = emailService;
        _environment = environment;
        _configuration = configuration;
        _logger = logger;
    }

    [Authorize(Policy = "Admin")]
    public async Task<IActionResult> AdminRequests() =>
        View(await _db.PrintRequests.OrderByDescending(r => r.SubmittedAt).ToListAsync());

    [HttpGet]
    public IActionResult RequestForm() => View(new PrintRequestInput());

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(PrivateUploadStore.RequestLimit)]
    public async Task<IActionResult> RequestForm(PrintRequestInput input, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return View(input);
        PrintRequest request;
        try
        {
            request = await new PrintRequestSubmission(_db, _environment, _configuration).SaveAsync(input, cancellationToken);
        }
        catch (InvalidDataException ex)
        {
            ModelState.AddModelError("", ex.Message);
            return View(input);
        }
        try { await _emailService.SendSubmissionConfirmationAsync(request); }
        catch (Exception ex) { _logger.LogError(ex, "Submission confirmation failed for request {RequestId}", request.Id); }
        return RedirectToAction(nameof(ThankYou));
    }

    [HttpGet]
    public IActionResult ThankYou() => View();

    [Authorize(Policy = "Admin")]
    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var request = await _db.PrintRequests.Include(r => r.Quote).FirstOrDefaultAsync(r => r.Id == id);
        return request == null ? NotFound() : View(request);
    }

    [Authorize(Policy = "Admin")]
    [HttpGet]
    public async Task<IActionResult> Download(int id, bool reference = false)
    {
        var request = await _db.PrintRequests.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id);
        if (request == null) return NotFound();
        var path = new PrivateUploadStore(_environment, _configuration).Find(reference ? request.ReferenceImagePath : request.FilePath, reference);
        if (path == null) return NotFound();
        Response.Headers.XContentTypeOptions = "nosniff";
        Response.Headers.CacheControl = "private, no-store";
        return PhysicalFile(path, "application/octet-stream", Path.GetFileName(path));
    }

    [Authorize(Policy = "Admin")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GenerateQuote(QuoteCalculationInput input)
    {
        if (!ModelState.IsValid || input.PrintRequestId <= 0 || input.ColorCount is < 1 or > 4
            || input.FilamentGrams is <= 0 or > 100000 || input.EstimatedPrintHours is <= 0 or > 10000
            || input.MaterialCostPerGram is < 0 or > 10000 || input.HourlyMachineRate is < 0 or > 10000
            || input.MultiColorFeePerColor is < 0 or > 10000 || input.BaseHandlingFee is < 0 or > 10000)
            return BadRequest("Invalid quote inputs.");
        var request = await _db.PrintRequests.Include(r => r.Quote).FirstOrDefaultAsync(r => r.Id == input.PrintRequestId);
        if (request == null) return NotFound();
        if (request.Status != QuoteStatus.PendingReview || request.Quote != null || input.ColorCount != request.RequestedColorsCount)
            return Conflict("This request cannot be quoted.");

        var material = input.FilamentGrams * input.MaterialCostPerGram;
        var labor = input.EstimatedPrintHours * input.HourlyMachineRate;
        var colors = (input.ColorCount - 1) * input.MultiColorFeePerColor;
        var total = Math.Round(material + labor + colors + input.BaseHandlingFee, 2, MidpointRounding.AwayFromZero);
        if (total < 0.50m || total > 999999.99m) return BadRequest("Quote total is outside the supported range.");
        _db.PrintQuotes.Add(new PrintQuote
        {
            PrintRequestId = request.Id, FilamentGrams = input.FilamentGrams, EstimatedPrintHours = input.EstimatedPrintHours,
            ColorCount = input.ColorCount, MaterialCostPerGram = input.MaterialCostPerGram, MaterialSubtotal = material,
            LaborMachineSubtotal = labor, MultiColorSurcharge = colors, BaseHandlingFee = input.BaseHandlingFee,
            FinalTotalPrice = total, CalculatedAt = DateTime.UtcNow
        });
        try { await _db.SaveChangesAsync(); }
        catch (DbUpdateException)
        {
            if (await _db.PrintQuotes.AsNoTracking().AnyAsync(q => q.PrintRequestId == request.Id))
                return Conflict("A quote already exists.");
            throw;
        }
        return RedirectToAction(nameof(Details), new { id = request.Id });
    }

    [Authorize(Policy = "Admin")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult SendEmailQuote(int requestId)
    {
        return QuotePaymentUnavailable();
    }

    [HttpGet]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public IActionResult PayQuote(string? token)
    {
        Response.Headers["Referrer-Policy"] = "no-referrer";
        return QuotePaymentUnavailable();
    }

    [HttpGet] public IActionResult PaymentSuccess() => View();
    [HttpGet] public IActionResult PaymentCancelled() => View();
    [HttpGet] public IActionResult QuoteAlreadyPaid() => View();

    private IActionResult QuotePaymentUnavailable()
    {
        Response.Headers.CacheControl = "no-store";
        return StatusCode(StatusCodes.Status503ServiceUnavailable,
            "Quote payment and payment-link emails are temporarily unavailable. Please contact the store.");
    }
}
