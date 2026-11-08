using System.Security.Claims;
using BrewStop.Data;
using BrewStop.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BrewStop.Controllers;

[Authorize]
public class OrderController(ApplicationDbContext db) : Controller
{
    // GET /Order  -> the logged-in user's order history
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        var orders = await db.Orders.AsNoTracking()
            .Where(o => o.UserId == userId)
            .Include(o => o.Items)
            .OrderByDescending(o => o.CreatedAtUtc)
            .ToListAsync();

        return View(orders);
    }

    // GET /Order/Details/5  -> full order with items and add-ons
    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        var order = await db.Orders.AsNoTracking()
            .Include(o => o.Items).ThenInclude(i => i.AddOns)
            .FirstOrDefaultAsync(o => o.Id == id && o.UserId == userId);

        return order is null ? NotFound() : View(order);
    }

    // GET /Order/Confirmation/5  (your existing action)
    [HttpGet]
    public async Task<IActionResult> Confirmation(int id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        var order = await db.Orders.AsNoTracking()
            .Include(o => o.Items).ThenInclude(i => i.AddOns)
            .FirstOrDefaultAsync(o => o.Id == id && o.UserId == userId);

        return order is null ? NotFound() : View(order);
    }

    // POST /Order/Cancel/5  -> only allowed while still Placed
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        var order = await db.Orders
            .FirstOrDefaultAsync(o => o.Id == id && o.UserId == userId);

        if (order is null) return NotFound();

        if (order.Status != OrderStatus.Placed)
        {
            TempData["Error"] = "This order can no longer be cancelled.";
            return RedirectToAction(nameof(Details), new { id });
        }

        order.Status = OrderStatus.Cancelled;
        await db.SaveChangesAsync();

        TempData["Success"] = "Your order was cancelled.";
        return RedirectToAction(nameof(Index));
    }
}