using System.Security.Claims;
using BrewStop.Data;
using BrewStop.Models;
using BrewStop.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BrewStop.Controllers;

[Authorize(Policy = "CustomerOnly")]
public class OrderController(ApplicationDbContext db, IQrTokenService qr, IAuditService audit, ICartService cart) : Controller
{
    private string? UserId => User.FindFirstValue(ClaimTypes.NameIdentifier);

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var orders = await db.Orders.AsNoTracking()
            .Where(o => o.UserId == UserId)
            .Include(o => o.Items)
            .OrderByDescending(o => o.CreatedAtUtc)
            .Take(50)
            .ToListAsync();

        return View(orders);
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var order = await db.Orders.AsNoTracking()
            .Include(o => o.Items).ThenInclude(i => i.AddOns)
            .FirstOrDefaultAsync(o => o.Id == id && o.UserId == UserId);

        if (order is null) return NotFound();

        ViewBag.QrToken = order.Status == OrderStatus.Ready
            ? await qr.GetTokenAsync(order.Id, HttpContext.RequestAborted) ?? await IssueAndGetAsync(order)
            : null;

        if (order.Status == OrderStatus.Claimed)
            ViewBag.Reviews = await db.ProductReviews.AsNoTracking()
                .Where(r => r.UserId == UserId && r.OrderId == order.Id)
                .ToDictionaryAsync(r => r.MenuItemId);

        return View(order);
    }

    private async Task<string?> IssueAndGetAsync(Order order)
    {
        await qr.IssueAsync(order, HttpContext.RequestAborted);
        return await qr.GetTokenAsync(order.Id, HttpContext.RequestAborted);
    }

    [HttpGet]
    public async Task<IActionResult> Confirmation(int id)
    {
        var order = await db.Orders.AsNoTracking()
            .Include(o => o.Items).ThenInclude(i => i.AddOns)
            .Include(o => o.Items).ThenInclude(i => i.MenuItem)
            .FirstOrDefaultAsync(o => o.Id == id && o.UserId == UserId);

        if (order is null) return NotFound();

        ViewBag.QrToken = await QrFor(order);
        return View(order);
    }

    private Task<string?> QrFor(Order o) =>
        o.Status is OrderStatus.Placed or OrderStatus.Preparing or OrderStatus.Ready
            ? qr.GetTokenAsync(o.Id)
            : Task.FromResult<string?>(null);

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

    [HttpGet]
    public async Task<IActionResult> RecentUpdates()
    {
        var rows = await db.Orders.AsNoTracking()
            .Where(o => o.UserId == UserId)
            .OrderByDescending(o => o.CreatedAtUtc)
            .Take(5)
            .Select(o => new { id = o.Id, status = o.Status.ToString(), total = o.Total, createdAtUtc = o.CreatedAtUtc })
            .ToListAsync(HttpContext.RequestAborted);
        return Json(rows);
    }

    [HttpGet]
    public async Task<IActionResult> Receipt(int id)
    {
        var order = await db.Orders.AsNoTracking()
            .Include(o => o.User)
            .Include(o => o.Items).ThenInclude(i => i.AddOns)
            .FirstOrDefaultAsync(o => o.Id == id && o.UserId == UserId);

        if (order is null) return NotFound();
        return View(order);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Reorder(int id)
    {
        var order = await db.Orders.AsNoTracking()
            .Include(o => o.Items).ThenInclude(i => i.AddOns)
            .FirstOrDefaultAsync(o => o.Id == id && o.UserId == UserId);

        if (order is null) return NotFound();

        var sizes = await db.ItemSizes.AsNoTracking().Where(s => s.IsActive).ToListAsync();
        var added = 0;
        var skipped = new List<string>();

        foreach (var i in order.Items)
        {
            int? sizeId = null;
            if (i.SizeName is not null)
            {
                sizeId = sizes.FirstOrDefault(s => s.Name == i.SizeName)?.Id;
                if (sizeId is null) { skipped.Add(i.ItemName); continue; }
            }

            var (ok, _) = await cart.AddAsync(i.MenuItemId, sizeId, i.AddOns.Select(a => a.AddOnId), i.Quantity);
            if (ok) added++; else skipped.Add(i.ItemName);
        }

        await audit.LogAsync(UserId, "OrderReordered", $"OrderId={id}; Added={added}; Skipped={skipped.Count}");

        if (skipped.Count > 0)
            TempData["Warning"] = "Skipped (out of stock or unavailable): " + string.Join(", ", skipped.Distinct()) + ".";
        if (added > 0)
            TempData["Success"] = $"{added} item(s) from order #{id} added to your cart.";

        return RedirectToAction("Index", "Cart");
    }
}