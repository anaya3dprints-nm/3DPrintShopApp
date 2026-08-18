using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ThreeDPrintStore.Models;
using ThreeDPrintStore.Services;

namespace ThreeDPrintStore.Controllers
{
    public class AdminController : Controller
    {
        private readonly StoreDbContext _context;
        private readonly PricingService _pricingService;
        private readonly ShippingService _shippingService;

        // Inject database context along with our pricing and shipping matrices
        public AdminController(StoreDbContext context, PricingService pricingService, ShippingService shippingService)
        {
            _context = context;
            _pricingService = pricingService;
            _shippingService = shippingService;
        }

        // 1. GET: /Admin/Dashboard
        [HttpGet]
        public async Task<IActionResult> Dashboard()
        {
            // Fetch quote orders, showing newest entries at the top
            var quotes = await _context.QuoteRequests
                                       .OrderByDescending(q => q.Id)
                                       .ToListAsync();
            return View(quotes);
        }

        // 2. POST: /Admin/CalculateQuote
        [HttpPost]
        public async Task<IActionResult> CalculateQuote(int quoteId, double gramsUsed, double printHours)
        {
            var quoteRequest = await _context.QuoteRequests.FindAsync(quoteId);
            if (quoteRequest == null)
            {
                return NotFound();
            }

            // Run the Pricing Service Matrix Core Calculations
            decimal printBasePrice = _pricingService.CalculateCustomPrintPrice(gramsUsed, printHours);
            
            // Run the Shipping Matrix checking against Albuquerque locations
            decimal shippingCost = _shippingService.CalculateShipping(quoteRequest.ShippingCity, quoteRequest.PostalCode);

            // Save metrics back into our database entity structure
            quoteRequest.CalculatedPrice = printBasePrice;
            quoteRequest.ShippingFee = shippingCost;
            quoteRequest.Status = "Calculated"; // Ready for client delivery pipeline

            _context.Entry(quoteRequest).State = EntityState.Modified;
            await _context.SaveChangesAsync();

            // Return cleanly back to the dashboard layout page view
            return RedirectToAction("Dashboard");
        }
    }
}
