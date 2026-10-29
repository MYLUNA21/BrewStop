using Microsoft.AspNetCore.Mvc;

namespace BrewStop.Controllers
{
    public class OrdersController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
