using System.Security.Claims;
using BrewStop.Data;
using BrewStop.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BrewStop.Controllers;

[Authorize(Policy = "CustomerOnly")]
[Route("api/notifications")]
public class NotificationsApiController(ApplicationDbContext db, ICustomerNotifier notifier) : Controller
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    [HttpGet("unread-count")]
    public async Task<IActionResult> UnreadCount(CancellationToken ct) =>
        Json(new { count = await notifier.UnreadCountAsync(UserId, ct) });

    [HttpGet("recent")]
    public async Task<IActionResult> Recent(CancellationToken ct)
    {
        var rows = await db.CustomerNotifications.AsNoTracking()
            .Where(n => n.UserId == UserId)
            .OrderByDescending(n => n.CreatedAtUtc).ThenByDescending(n => n.Id)
            .Take(8)
            .ToListAsync(ct);

        return Json(rows.Select(n => new
        {
            id = n.Id,
            type = n.Type.ToString(),
            title = n.Title,
            message = n.Message,
            orderId = n.OrderId,
            createdAtUtc = n.CreatedAtUtc,
            isRead = n.IsRead
        }));
    }

    [HttpPost("mark-all-read")]
    public async Task<IActionResult> MarkAllRead(CancellationToken ct)
    {
        await notifier.MarkAllReadAsync(UserId, ct);
        return NoContent();
    }
}