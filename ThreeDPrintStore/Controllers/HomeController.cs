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
        public IActionResult Home()
        {
            //load the homepage view
            return View("Home");
        }

        // GET: /Catalog
        public async Task<IActionResult> Index()
        {
            var activeInventory = await _context.Products.ToListAsync();
            return View("Index", activeInventory); //Index.cshtml = Catalog page
        }
    }
}
