using System.Security.Claims;
using BrewStop.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BrewStop.Controllers;

[Authorize]
public class OrderController(ApplicationDbContext db) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Confirmation(int id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        var order = await db.Orders.AsNoTracking()
            .Include(o => o.Items).ThenInclude(i => i.AddOns)
            .FirstOrDefaultAsync(o => o.Id == id && o.UserId == userId);

        return order is null ? NotFound() : View(order);
    }
}