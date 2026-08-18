using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ThreeDPrintStore.Models;

namespace ThreeDPrintStore.Controllers
{
    public class HomeController : Controller
    {
        private readonly StoreDbContext _context;

        public HomeController(StoreDbContext context)
        {
            _context = context;
        }

        // GET: / (Homepage)
        public async Task<IActionResult> Index()
        {
            // Pull all catalog items out of SQLite storage
            var activeInventory = await _context.Products.ToListAsync();
            return View(activeInventory);
        }
    }
}
