using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using ThreeDPrintStore.Data;
using ThreeDPrintStore.Models;
using ThreeDPrintStore.Services;

namespace ThreeDPrintStore.Controllers;

[Route("api/[controller]")]
[ApiController]
public class PrintRequestsController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly IWebHostEnvironment _environment;
    private readonly IConfiguration _configuration;

    public PrintRequestsController(ApplicationDbContext db, IWebHostEnvironment environment, IConfiguration configuration)
    {
        _db = db;
        _environment = environment;
        _configuration = configuration;
    }

    [HttpGet("antiforgery")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public IActionResult AntiforgeryToken([FromServices] IAntiforgery antiforgery)
    {
        var tokens = antiforgery.GetAndStoreTokens(HttpContext);
        return Ok(new { token = tokens.RequestToken, headerName = tokens.HeaderName, formFieldName = tokens.FormFieldName });
    }

    [HttpPost("submit")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(PrivateUploadStore.RequestLimit)]
    public async Task<IActionResult> SubmitPrintRequests([FromForm] PrintRequestInput input, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        try
        {
            var request = await new PrintRequestSubmission(_db, _environment, _configuration).SaveAsync(input, cancellationToken);
            return Ok(new { message = "Print request submitted successfully!", requestId = request.Id });
        }
        catch (InvalidDataException ex)
        {
            ModelState.AddModelError("File", ex.Message);
            return ValidationProblem(ModelState);
        }
    }
}
