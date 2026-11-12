using System.Security.Claims;
using BrewStop.Data;
using BrewStop.Models;
using BrewStop.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BrewStop.Controllers;

[Authorize]
public class OrderController(ApplicationDbContext db, IQrTokenService qr, IAuditService audit) : Controller
{
    private string? UserId => User.FindFirstValue(ClaimTypes.NameIdentifier);

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var orders = await db.Orders.AsNoTracking()
            .Where(o => o.UserId == UserId)
            .Include(o => o.Items)
            .OrderByDescending(o => o.CreatedAtUtc)
            .ToListAsync();

        return View(orders);
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var order = await db.Orders.AsNoTracking()
            .Include(o => o.Items).ThenInclude(i => i.AddOns)
            .FirstOrDefaultAsync(o => o.Id == id && o.UserId == UserId);

        return order is null ? NotFound() : View(order);
    }

    [HttpGet]
    public async Task<IActionResult> Confirmation(int id)
    {
        var order = await db.Orders.AsNoTracking()
            .Include(o => o.Items).ThenInclude(i => i.AddOns)
            .Include(o => o.Items).ThenInclude(i => i.MenuItem)
            .FirstOrDefaultAsync(o => o.Id == id && o.UserId == UserId);

        return order is null ? NotFound() : View(order);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id)
    {
        var order = await db.Orders.Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == id && o.UserId == UserId);

        if (order is null) return NotFound();

        if (order.Status != OrderStatus.Placed)
        {
            TempData["Error"] = "This order can no longer be cancelled.";
            return RedirectToAction(nameof(Details), new { id });
        }

        await using var tx = await db.Database.BeginTransactionAsync();

        foreach (var g in order.Items.GroupBy(i => i.MenuItemId))
        {
            var qty = g.Sum(i => i.Quantity);
            await db.InventoryItems.Where(i => i.MenuItemId == g.Key)
                .ExecuteUpdateAsync(s => s.SetProperty(i => i.Quantity, i => i.Quantity + qty));
        }

        order.Status = OrderStatus.Cancelled;
        db.AuditLogs.Add(new AuditLog
        {
            UserId = UserId,
            Action = "OrderCancelled",
            Details = $"OrderId={order.Id}",
            CreatedAtUtc = DateTime.UtcNow
        });

        await qr.RevokeAsync(order.Id);                                              
        audit.Add(UserId, "OrderStatusChanged", $"OrderId={order.Id}; Placed->Cancelled"); 

        await db.SaveChangesAsync();
        await tx.CommitAsync();

        TempData["Success"] = "Your order was cancelled.";
        return RedirectToAction(nameof(Index));
    }
}