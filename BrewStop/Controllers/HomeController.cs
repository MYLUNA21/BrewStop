using System.Diagnostics;
using System.Security.Claims;
using BrewStop.Models;
using BrewStop.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BrewStop.Controllers;

public class HomeController(IAuditService audit) : Controller
{
    public IActionResult Index()
    {
        if (User.IsInRole(AppRoles.Admin))
            return RedirectToAction("Dashboard", "Admin");

        return View();
    }

    public IActionResult Privacy() => View();

    [HttpGet, AllowAnonymous]
    public IActionResult Support() => View(new SupportViewModel());

    [HttpPost, AllowAnonymous, ValidateAntiForgeryToken]
    public async Task<IActionResult> Support(SupportViewModel m)
    {
        if (!ModelState.IsValid) return View(m);

        // Stored in AuditLog (no new table/migration). Swap for IEmailService to notify the shop by email.
        await audit.LogAsync(User.FindFirstValue(ClaimTypes.NameIdentifier), "SupportMessage",
            $"Topic={m.Topic}; From={m.Name} <{m.Email}>; Msg={m.Message.Replace('\r', ' ').Replace('\n', ' ')}",
            HttpContext.RequestAborted);

        TempData["Success"] = "Thanks! Your message was sent. We'll reply by email soon.";
        return RedirectToAction(nameof(Support), null, "contact");
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
        => View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
}