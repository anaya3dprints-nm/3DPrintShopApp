using Microsoft.AspNetCore.Mvc;
using Custom3DPrints.Data;
using Custom3DPrints.Models;

namespace Custom3DPrints.Controllers
{
	[Route("api/[controller]")]
	[ApiController]
	public class PrintRequestsController
	{
		private readonly ApplicationDbContext _db;
		private readonly IWebHostEnvironment _env;
		
		public PrintRequestsController(ApplicationDbContext db, IWebHostEnvironment env)
		{
			_db = db;
			_env = env;
		}
		
		[HttpPost("submit")]
		public async Task<IActionResult> SubmitPrintRequests (
			[FromForm] string customerName,
			[FromForm] string customerEmail,
			[FromForm] string? projectDescription,
			[FromForm] string materialType,
			[FromForm] int requestedColorsCount,
			[FromForm] IFormFile modelFile)
			{
				if (modelFile == null || modelFile == 0)
				{
					return.BadRequest("A valid 3D Model File is required.");
				}
				
				//secure file storage path setup
				var uploadsFolder = Path.Combine(_env.ContentRootPath, "App_Data", "Uploads");
				if (!Directory.Exists(uploadsFolder))
				{
					Directory.CreateDirectory(uploadsFolder);
				}
				
				var uniqueFileName = $"{Guid.NewGuid()}_{Path.GetFileName(modelFile.FileName))}";
				var filePath = Path.Combine(uploadsFolder, uniqueFileName);
				
				using (var stream = new FileStream(filePath, FileMode.Create))
				{
					await modelFile.CopyToAsync(stream);
				}
				
				//Save database
				var request = new PrintRequest
				{
					CustomerName = customerName,
					CustomerEmail = customerEmail,
					ProjectDescription = projectDescription ?? string.Empty,
					FilePath = filePath,
					MaterialType = materialType,
					RequestedColorsCount = requestedColorsCount,
					Status = QuoteStatus.PendingReview,
					SubmittedAt = DateTime.UtcNow
				};
				
				_db.PrintRequests.Add(request);
				await _db.SaveChangesAsync();
				
				return Ok(new { message = "Print request submitted successfully!", requestId = request.Id});
			}	
		
	}
}
