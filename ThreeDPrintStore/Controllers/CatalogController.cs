using Microsoft.AspNetCore.Mvc;

namespace Anaya3DPrints.Controllers
{
    public class CatalogController : Controller
    {
        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }
    }
}
