using BrewStop.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BrewStop.Controllers.Admin;

[Authorize(Roles = AppRoles.Admin)]
[Route("Admin")]
public class AdminController : Controller
{
    [HttpGet(""), HttpGet("Index")]
    public IActionResult Index() => View("~/Views/Admin/Index.cshtml");

    [HttpGet("Menu")]
    public IActionResult Menu() => RedirectToAction("Index", "MenuItems");
}