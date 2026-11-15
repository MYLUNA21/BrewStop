using System.Security.Claims;
using BrewStop.Data;
using BrewStop.Models;
using BrewStop.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BrewStop.Controllers.Admin;

[Authorize(Roles = AppRoles.Admin)]
[Route("Admin/Inventory")]
public class InventoryController(ApplicationDbContext db, IAuditService audit) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index() =>
        View("~/Views/Admin/Inventory/Index.cshtml",
            await db.InventoryItems.AsNoTracking()
                .Include(i => i.MenuItem).ThenInclude(m => m.Category)
                .OrderBy(i => i.MenuItem.Category.Name).ThenBy(i => i.MenuItem.Name)
                .ToListAsync());

    [HttpPost("Update/{id:int}"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(int id, int quantity)
    {
        if (quantity < 0)
        {
            TempData["Error"] = "Quantity cannot be negative.";
            return RedirectToAction(nameof(Index));
        }

        var item = await db.InventoryItems.Include(i => i.MenuItem).FirstOrDefaultAsync(i => i.Id == id);
        if (item is null) return NotFound();

        var old = item.Quantity;
        item.Quantity = quantity;

        audit.Add(User.FindFirstValue(ClaimTypes.NameIdentifier), "InventoryUpdated",
            $"MenuItemId={item.MenuItemId}; {old}->{quantity}");

        await db.SaveChangesAsync();

        TempData["Success"] = $"{item.MenuItem.Name} stock set to {quantity}.";
        return RedirectToAction(nameof(Index));
    }
}