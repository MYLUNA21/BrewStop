using System.Security.Claims;
using BrewStop.Data;
using BrewStop.Models;
using BrewStop.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BrewStop.Controllers.Api;

public record MenuItemDto(int Id, string Name, string Description, string CategoryName,
    decimal BasePrice, int StockQuantity, bool IsAvailable);

public record OrderStatusDto(int OrderId, string Status, DateTime PickupSlot,
    string PaymentMethod, string PaymentStatus, DateTime UpdatedAt);

public record ScanRequest(string? Token);

[ApiController]
[Produces("application/json")]
[IgnoreAntiforgeryToken] // global AutoValidateAntiforgeryToken is for MVC forms only
[Route("api")]
public class BrewStopApiController(ApplicationDbContext db, IQrTokenService qr) : ControllerBase
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    [AllowAnonymous]
    [HttpGet("menu")]
    [ProducesResponseType(typeof(List<MenuItemDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMenu(CancellationToken ct)
    {
        var rows = await (
            from m in db.MenuItems.AsNoTracking()
            join i in db.InventoryItems.AsNoTracking() on m.Id equals i.MenuItemId into inv
            from i in inv.DefaultIfEmpty()
            orderby m.Category.Name, m.Name
            select new
            {
                m.Id,
                m.Name,
                m.Description,
                CategoryName = m.Category.Name,
                BasePrice = m.Price,
                Stock = i != null ? i.Quantity : 0
            }).ToListAsync(ct);

        return Ok(rows.Select(r => new MenuItemDto(
            r.Id, r.Name, r.Description, r.CategoryName, r.BasePrice, r.Stock, r.Stock > 0)));
    }

    [Authorize]
    [HttpGet("orders/{id:int}/status")]
    [ProducesResponseType(typeof(OrderStatusDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetOrderStatus(int id, CancellationToken ct)
    {
        var o = await db.Orders.AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new
            {
                x.Id,
                x.UserId,
                x.Status,
                x.PickupAtUtc,
                x.PaymentMethod,
                x.PaymentStatus,
                x.CreatedAtUtc,
                x.PaidAtUtc
            })
            .FirstOrDefaultAsync(ct);

        if (o is null)
            return NotFound(new { success = false, message = "Order not found." });

        var isStaff = User.IsInRole(AppRoles.Barista) || User.IsInRole(AppRoles.Admin);
        if (o.UserId != UserId && !isStaff)
            return StatusCode(StatusCodes.Status403Forbidden,
                new { success = false, message = "You do not have access to this order." });

        var exact = $"OrderId={id}";
        var prefix = $"OrderId={id};";
        var lastAudit = await db.AuditLogs.AsNoTracking()
            .Where(a => (a.Action == "OrderStatusChanged" || a.Action == "QrScanAccepted")
                     && (a.Details == exact || a.Details!.StartsWith(prefix)))
            .MaxAsync(a => (DateTime?)a.CreatedAtUtc, ct);

        var updated = new[] { o.CreatedAtUtc, o.PaidAtUtc ?? DateTime.MinValue, lastAudit ?? DateTime.MinValue }.Max();

        return Ok(new OrderStatusDto(
            o.Id, o.Status.ToString(),
            DateTime.SpecifyKind(o.PickupAtUtc, DateTimeKind.Utc),
            o.PaymentMethod.ToString(), o.PaymentStatus.ToString(),
            DateTime.SpecifyKind(updated, DateTimeKind.Utc)));
    }

    [Authorize(Roles = $"{AppRoles.Barista},{AppRoles.Admin}")]
    [HttpPost("barista/scan")]
    public async Task<IActionResult> Scan([FromBody] ScanRequest? req, CancellationToken ct)
    {
        var token = req?.Token?.Trim();
        if (string.IsNullOrWhiteSpace(token))
            return BadRequest(new { success = false, message = "QR token is required." });
        if (token.Length > 512)
            return BadRequest(new { success = false, message = "Invalid QR token." });

        var (result, orderId) = await qr.ClaimAsync(token, UserId, ct);

        if (result == ScanResult.Ok)
            return Ok(new { success = true, message = $"Order #{orderId} claimed.", orderId });

        var msg = result switch
        {
            ScanResult.AlreadyUsed => $"Order #{orderId} was already claimed.",
            ScanResult.Expired => $"Pickup code for order #{orderId} has expired.",
            ScanResult.Revoked => $"Order #{orderId} was cancelled.",
            ScanResult.WrongStatus => $"Order #{orderId} is not Ready yet.",
            _ => "Invalid QR token."
        };
        return BadRequest(new { success = false, message = msg, orderId });
    }
}