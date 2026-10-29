using Microsoft.AspNetCore.Mvc;

namespace BrewStop.Controllers
{
    public class ExampleController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
