using System.Security.Claims;
using BrewStop.Data;
using BrewStop.Hubs;
using BrewStop.Models;
using BrewStop.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace BrewStop.Controllers;

[Authorize(Roles = $"{AppRoles.Barista},{AppRoles.Admin}")]
[Route("Barista")]
public class BaristaController(
    ApplicationDbContext db,
    IQrTokenService qr,
    IAuditService audit,
    IHubContext<OrderHub> hub) : Controller
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    [HttpGet("")]
    public async Task<IActionResult> Index() =>
        View(await db.Orders.AsNoTracking()
            .Where(o => o.Status == OrderStatus.Placed || o.Status == OrderStatus.Preparing || o.Status == OrderStatus.Ready)
            .Include(o => o.User)
            .Include(o => o.Items).ThenInclude(i => i.AddOns)
            .OrderBy(o => o.PickupAtUtc)
            .ToListAsync());

    [HttpPost("Advance/{id:int}")]
    public async Task<IActionResult> Advance(int id)
    {
        var order = await db.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.Id == id);
        if (order is null) return NotFound();

        OrderStatus? next = order.Status switch
        {
            OrderStatus.Placed => OrderStatus.Preparing,
            OrderStatus.Preparing => OrderStatus.Ready,
            _ => null
        };

        if (next is null)
        {
            TempData["Error"] = $"Order #{id} cannot be advanced from {order.Status}.";
            return RedirectToAction(nameof(Index));
        }

        var from = order.Status;
        var to = next.Value;

        var rows = await db.Orders.Where(o => o.Id == id && o.Status == from)
            .ExecuteUpdateAsync(s => s.SetProperty(o => o.Status, to));

        if (rows == 0)
        {
            TempData["Error"] = $"Order #{id} changed in the meantime. Please review the queue.";
            return RedirectToAction(nameof(Index));
        }

        await audit.LogAsync(UserId, "OrderStatusChanged", $"OrderId={id}; {from}->{to}");
        await hub.Clients.Group($"user:{order.UserId}")
            .SendAsync("OrderStatusChanged", new { orderId = id, status = to.ToString() });

        TempData["Success"] = $"Order #{id} is now {to}.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("Scan")]
    public async Task<IActionResult> Scan(string? token)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 512)
        {
            TempData["Error"] = "Scan or paste a pickup QR token.";
            return RedirectToAction(nameof(Index));
        }

        var (result, orderId) = await qr.ClaimAsync(token.Trim(), UserId, HttpContext.RequestAborted);

        if (result == ScanResult.Ok)
        {
            var ownerId = await db.Orders.AsNoTracking()
                .Where(o => o.Id == orderId).Select(o => o.UserId).FirstOrDefaultAsync();
            if (ownerId is not null)
                await hub.Clients.Group($"user:{ownerId}")
                    .SendAsync("OrderStatusChanged", new { orderId, status = nameof(OrderStatus.Claimed) });

            TempData["Success"] = $"Order #{orderId} claimed. Collect payment at the counter.";
        }
        else
            TempData["Error"] = result switch
            {
                ScanResult.AlreadyUsed => $"Rejected: order #{orderId} was already claimed.",
                ScanResult.Expired => $"Rejected: pickup code for order #{orderId} has expired.",
                ScanResult.Revoked => $"Rejected: order #{orderId} was cancelled.",
                ScanResult.WrongStatus => $"Rejected: order #{orderId} is not Ready yet.",
                _ => "Rejected: invalid QR code."
            };

        return RedirectToAction(nameof(Index));
    }
}