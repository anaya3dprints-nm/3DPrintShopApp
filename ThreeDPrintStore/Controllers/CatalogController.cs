using Microsoft.AspNetCore.Mvc;

namespace ThreeDPrintStore.Controllers
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
