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
    IHubContext<OrderHub> hub,
    IPickupSlotService slots,
    ICustomerNotifier notifier) : Controller
{
    public const int LowStockThreshold = 5;

    private static readonly string[] ReturnTargets = ["Queue", "Preparation", "Status"];
    private static readonly string[] DashboardActions =
        ["OrderPlaced", "OrderCancelled", "OrderStatusChanged", "QrScanAccepted", "QrScanRejected"];

    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    private IQueryable<Order> Board() => db.Orders.AsNoTracking()
        .Include(o => o.User)
        .Include(o => o.Items).ThenInclude(i => i.AddOns);

    private static string? Field(string? details, string key) =>
        details?.Split(';', StringSplitOptions.TrimEntries)
            .FirstOrDefault(p => p.StartsWith(key + "=", StringComparison.Ordinal))?[(key.Length + 1)..];

    // ---------------- Dashboard ----------------
    [HttpGet(""), HttpGet("Dashboard")]
    public async Task<IActionResult> Index()
    {
        var nowUtc = DateTime.UtcNow;
        var localNow = slots.ToLocal(nowUtc);
        var dayStart = nowUtc - (localNow - localNow.Date);

        var topRows = await db.Orders.AsNoTracking()
            .Where(o => o.CreatedAtUtc >= dayStart && o.Status != OrderStatus.Cancelled)
            .SelectMany(o => o.Items)
            .GroupBy(i => i.ItemName)
            .Select(g => new { Name = g.Key, Qty = g.Sum(x => x.Quantity) })
            .OrderByDescending(x => x.Qty)
            .Take(5)
            .ToListAsync();

        var logs = await db.AuditLogs.AsNoTracking()
            .Where(a => DashboardActions.Contains(a.Action))
            .OrderByDescending(a => a.CreatedAtUtc)
            .Take(40)
            .ToListAsync();

        var recent = new List<ActivityDto>();
        foreach (var a in logs)
        {
            var id = Field(a.Details, "OrderId");
            var transition = a.Details?.Split(';', StringSplitOptions.TrimEntries).Skip(1).FirstOrDefault() ?? "";
            switch (a.Action)
            {
                case "OrderPlaced":
                    recent.Add(new ActivityDto("New order placed", $"Order #{id} entered the queue", "fa-bag-shopping", a.CreatedAtUtc));
                    break;
                case "OrderCancelled":
                    recent.Add(new ActivityDto("Order cancelled", $"Order #{id} was cancelled by the customer", "fa-ban", a.CreatedAtUtc));
                    break;
                case "OrderStatusChanged" when transition.EndsWith("->Cancelled") || transition.EndsWith("->Claimed"):
                    break; // already covered by OrderCancelled / QrScanAccepted
                case "OrderStatusChanged":
                    recent.Add(new ActivityDto("Status updated", $"Order #{id}: {transition.Replace("->", " → ")}", "fa-arrows-rotate", a.CreatedAtUtc));
                    break;
                case "QrScanAccepted":
                    recent.Add(new ActivityDto("Order picked up", $"Order #{id} claimed with a verified QR code", "fa-circle-check", a.CreatedAtUtc));
                    break;
                case "QrScanRejected":
                    recent.Add(new ActivityDto("QR scan rejected", $"Order #{id}: {Field(a.Details, "Reason") ?? "invalid code"}", "fa-triangle-exclamation", a.CreatedAtUtc));
                    break;
            }
            if (recent.Count == 12) break;
        }

        var vm = new BaristaDashboardViewModel
        {
            LocalNow = localNow,
            Placed = await db.Orders.CountAsync(o => o.Status == OrderStatus.Placed),
            Preparing = await db.Orders.CountAsync(o => o.Status == OrderStatus.Preparing),
            Ready = await db.Orders.CountAsync(o => o.Status == OrderStatus.Ready),
            OrdersToday = await db.Orders.CountAsync(o => o.CreatedAtUtc >= dayStart),
            ClaimedToday = await db.Orders.CountAsync(o => o.CreatedAtUtc >= dayStart && o.Status == OrderStatus.Claimed),
            CancelledToday = await db.Orders.CountAsync(o => o.CreatedAtUtc >= dayStart && o.Status == OrderStatus.Cancelled),
            LowStockCount = await db.InventoryItems.CountAsync(i => i.Quantity <= LowStockThreshold),
            TopItems = topRows.Select(r => new TopItemDto(r.Name, r.Qty)).ToList(),
            Recent = recent,
            OpenShift = await db.BaristaShifts.AsNoTracking()
                .FirstOrDefaultAsync(s => s.BaristaUserId == UserId && s.ClockInAt != null && s.ClockOutAt == null)
        };
        return View("Dashboard", vm);
    }

    // ---------------- Order Queue / Preparation / Status ----------------
    [HttpGet("Queue")]
    public async Task<IActionResult> Queue() =>
        View(await Board().Where(o => o.Status == OrderStatus.Placed).OrderBy(o => o.CreatedAtUtc).ToListAsync());

    [HttpGet("Preparation")]
    public async Task<IActionResult> Preparation() =>
        View(await Board().Where(o => o.Status == OrderStatus.Preparing).OrderBy(o => o.PickupAtUtc).ToListAsync());

    [HttpGet("Status")]
    public async Task<IActionResult> Status() =>
        View(await Board()
            .Where(o => o.Status == OrderStatus.Placed || o.Status == OrderStatus.Preparing || o.Status == OrderStatus.Ready)
            .OrderBy(o => o.PickupAtUtc)
            .ToListAsync());

    [HttpPost("Advance/{id:int}")]
    public async Task<IActionResult> Advance(int id, string? returnTo)
    {
        var target = ReturnTargets.Contains(returnTo) ? returnTo! : "Status";

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
            return RedirectToAction(target);
        }

        var from = order.Status;
        var to = next.Value;

        var rows = await db.Orders.Where(o => o.Id == id && o.Status == from)
            .ExecuteUpdateAsync(s => s.SetProperty(o => o.Status, to));

        if (rows == 0)
        {
            TempData["Error"] = $"Order #{id} changed in the meantime. Please review the queue.";
            return RedirectToAction(target);
        }

        await audit.LogAsync(UserId, "OrderStatusChanged", $"OrderId={id}; {from}->{to}");
        await hub.Clients.Group($"user:{order.UserId}")
            .SendAsync("OrderStatusChanged", new { orderId = id, status = to.ToString() });
        await notifier.OrderStatusAsync(id, order.UserId, to);
        await hub.Clients.Group("staff").SendAsync("QueueChanged");

        TempData["Success"] = to == OrderStatus.Preparing
            ? $"Order #{id} accepted and now Preparing."
            : $"Order #{id} is Ready for pickup.";
        return RedirectToAction(target);
    }

    [HttpPost("Scan")]
    public async Task<IActionResult> Scan(string? token)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 512)
        {
            TempData["Error"] = "Scan or paste a pickup QR token.";
            return RedirectToAction(nameof(Status));
        }

        var (result, orderId) = await qr.ClaimAsync(token.Trim(), UserId, HttpContext.RequestAborted);

        if (result == ScanResult.Ok && orderId.HasValue)
        {
            var ownerId = await db.Orders.AsNoTracking()
                .Where(o => o.Id == orderId.Value).Select(o => o.UserId).FirstOrDefaultAsync();
            if (ownerId is not null)
            {
                await hub.Clients.Group($"user:{ownerId}")
                    .SendAsync("OrderStatusChanged", new { orderId = orderId.Value, status = nameof(OrderStatus.Claimed) });
                await notifier.OrderStatusAsync(orderId.Value, ownerId, OrderStatus.Claimed);
            }
            await hub.Clients.Group("staff").SendAsync("QueueChanged");

            TempData["Success"] = $"Order #{orderId.Value} claimed. Collect payment at the counter.";
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

        return RedirectToAction(nameof(Status));
    }

    // ---------------- Sidebar badge counts ----------------
    [HttpGet("Counts")]
    public async Task<IActionResult> Counts()
    {
        var since = DateTime.UtcNow.AddHours(-24);
        var placed = await db.Orders.CountAsync(o => o.Status == OrderStatus.Placed);
        var preparing = await db.Orders.CountAsync(o => o.Status == OrderStatus.Preparing);
        var low = await db.InventoryItems.CountAsync(i => i.Quantity <= LowStockThreshold);
        var notes = await db.Orders.CountAsync(o =>
            (o.Status == OrderStatus.Placed || o.Status == OrderStatus.Preparing) && o.Note != null && o.Note != "");
        var cancelled = await db.AuditLogs.CountAsync(a => a.Action == "OrderCancelled" && a.CreatedAtUtc >= since);
        return Json(new { placed, preparing, alerts = low + notes + cancelled });
    }

    // ---------------- Notifications ----------------
    [HttpGet("Notifications")]
    public async Task<IActionResult> Notifications()
    {
        var since = DateTime.UtcNow.AddHours(-24);
        var list = new List<BaristaAlertDto>();

        var stock = await db.InventoryItems.AsNoTracking()
            .Where(i => i.Quantity <= LowStockThreshold)
            .OrderBy(i => i.Quantity).ThenBy(i => i.MenuItem.Name)
            .Select(i => new { i.MenuItem.Name, i.Quantity })
            .ToListAsync();
        list.AddRange(stock.Select(s => new BaristaAlertDto(
            "LowStock",
            s.Quantity == 0 ? "Out of stock" : "Low stock",
            s.Quantity == 0 ? $"{s.Name} is sold out. Customers cannot order it." : $"{s.Name}: only {s.Quantity} left.",
            null, null, s.Quantity == 0)));

        var newOrders = await db.Orders.AsNoTracking()
            .Where(o => o.Status == OrderStatus.Placed)
            .OrderByDescending(o => o.CreatedAtUtc).Take(30)
            .Select(o => new { o.Id, o.User.FullName, o.CreatedAtUtc, Qty = o.Items.Sum(i => i.Quantity) })
            .ToListAsync();
        list.AddRange(newOrders.Select(o => new BaristaAlertDto(
            "NewOrder", $"New order #{o.Id}", $"{o.FullName} · {o.Qty} item(s) waiting to be accepted", o.Id, o.CreatedAtUtc)));

        var noted = await db.Orders.AsNoTracking()
            .Where(o => (o.Status == OrderStatus.Placed || o.Status == OrderStatus.Preparing) && o.Note != null && o.Note != "")
            .OrderByDescending(o => o.CreatedAtUtc).Take(30)
            .Select(o => new { o.Id, o.Note, o.CreatedAtUtc })
            .ToListAsync();
        list.AddRange(noted.Select(o => new BaristaAlertDto(
            "Note", $"Special instructions · Order #{o.Id}", o.Note!, o.Id, o.CreatedAtUtc)));

        var cancelled = await db.AuditLogs.AsNoTracking()
            .Where(a => a.Action == "OrderCancelled" && a.CreatedAtUtc >= since)
            .OrderByDescending(a => a.CreatedAtUtc).Take(30)
            .ToListAsync();
        foreach (var c in cancelled)
        {
            int.TryParse(Field(c.Details, "OrderId"), out var oid);
            list.Add(new BaristaAlertDto("Cancelled", $"Order #{oid} cancelled", "The customer cancelled before preparation.", oid, c.CreatedAtUtc));
        }

        var sorted = list.OrderByDescending(a => a.AtUtc == null).ThenByDescending(a => a.AtUtc).ToList();
        return View(sorted);
    }

    // ---------------- Coffee Recipe Guide (read-only reference) ----------------
    [HttpGet("Recipes")]
    public async Task<IActionResult> Recipes() =>
        View(new RecipeGuideViewModel
        {
            Categories = await db.Categories.AsNoTracking().Include(c => c.MenuItems).OrderBy(c => c.Id).ToListAsync(),
            Sizes = await db.ItemSizes.AsNoTracking().Where(s => s.IsActive).OrderBy(s => s.SortOrder).ToListAsync(),
            AddOns = await db.AddOns.AsNoTracking().Where(a => a.IsActive).OrderBy(a => a.Name).ToListAsync()
        });

    // ---------------- Shift Management ----------------
    [HttpGet("Shifts")]
    public async Task<IActionResult> Shifts()
    {
        var uid = UserId;
        var nowUtc = DateTime.UtcNow;
        var localNow = slots.ToLocal(nowUtc);
        var dayStart = nowUtc - (localNow - localNow.Date);
        var weekStartLocal = localNow.Date.AddDays(-(((int)localNow.DayOfWeek + 6) % 7));
        var weekStart = nowUtc - (localNow - weekStartLocal);

        var weekShifts = await db.BaristaShifts.AsNoTracking()
            .Where(s => s.BaristaUserId == uid && s.ClockInAt >= weekStart)
            .ToListAsync();

        decimal Hours(BaristaShift s) =>
            s.ClockOutAt is null ? (decimal)(nowUtc - s.ClockInAt!.Value).TotalHours : s.TotalHours ?? 0m;

        return View(new ShiftsViewModel
        {
            Open = await db.BaristaShifts.AsNoTracking()
                .FirstOrDefaultAsync(s => s.BaristaUserId == uid && s.ClockInAt != null && s.ClockOutAt == null),
            Upcoming = await db.BaristaShifts.AsNoTracking()
                .Where(s => s.BaristaUserId == uid && s.ClockInAt == null && s.ShiftStatus == ShiftStatus.Scheduled && s.ScheduledEnd >= nowUtc)
                .OrderBy(s => s.ScheduledStart).Take(10).ToListAsync(),
            History = await db.BaristaShifts.AsNoTracking()
                .Where(s => s.BaristaUserId == uid && s.ClockOutAt != null)
                .OrderByDescending(s => s.ClockInAt).Take(30).ToListAsync(),
            HoursThisWeek = Math.Round(weekShifts.Sum(s => Hours(s)), 2),
            HoursToday = Math.Round(weekShifts.Where(s => s.ClockInAt >= dayStart).Sum(s => Hours(s)), 2)
        });
    }

    [HttpPost("Shifts/ClockIn")]
    public async Task<IActionResult> ClockIn()
    {
        var uid = UserId;
        var now = DateTime.UtcNow;

        if (await db.BaristaShifts.AnyAsync(s => s.BaristaUserId == uid && s.ClockInAt != null && s.ClockOutAt == null))
        {
            TempData["Error"] = "You are already clocked in.";
            return RedirectToAction(nameof(Shifts));
        }

        // Match an assigned shift (clock-in allowed up to 2 hours early); otherwise record an unscheduled shift.
        var assignedId = await db.BaristaShifts
            .Where(s => s.BaristaUserId == uid && s.ClockInAt == null && s.ShiftStatus == ShiftStatus.Scheduled
                        && s.ScheduledStart <= now.AddHours(2) && s.ScheduledEnd >= now)
            .OrderBy(s => s.ScheduledStart)
            .Select(s => s.Id)
            .FirstOrDefaultAsync();

        int shiftId;
        if (assignedId != 0)
        {
            var rows = await db.BaristaShifts.Where(s => s.Id == assignedId && s.ClockInAt == null)
                .ExecuteUpdateAsync(u => u
                    .SetProperty(s => s.ClockInAt, now)
                    .SetProperty(s => s.ShiftStatus, ShiftStatus.InProgress));
            if (rows == 0)
            {
                TempData["Error"] = "That shift was just updated. Please try again.";
                return RedirectToAction(nameof(Shifts));
            }
            shiftId = assignedId;
        }
        else
        {
            var shift = new BaristaShift
            {
                BaristaUserId = uid,
                ScheduledStart = now,
                ScheduledEnd = now,   // start == end marks an unscheduled shift
                ClockInAt = now,
                ShiftStatus = ShiftStatus.InProgress
            };
            db.BaristaShifts.Add(shift);
            await db.SaveChangesAsync();
            shiftId = shift.Id;
        }

        await audit.LogAsync(uid, "ShiftClockIn", $"ShiftId={shiftId}");
        TempData["Success"] = "You are clocked in. Have a great shift!";
        return RedirectToAction(nameof(Shifts));
    }

    [HttpPost("Shifts/ClockOut")]
    public async Task<IActionResult> ClockOut()
    {
        var uid = UserId;
        var now = DateTime.UtcNow;

        var open = await db.BaristaShifts.AsNoTracking()
            .FirstOrDefaultAsync(s => s.BaristaUserId == uid && s.ClockInAt != null && s.ClockOutAt == null);
        if (open is null)
        {
            TempData["Error"] = "You are not clocked in.";
            return RedirectToAction(nameof(Shifts));
        }

        var hours = Math.Round((decimal)(now - open.ClockInAt!.Value).TotalHours, 2);
        var rows = await db.BaristaShifts.Where(s => s.Id == open.Id && s.ClockOutAt == null)
            .ExecuteUpdateAsync(u => u
                .SetProperty(s => s.ClockOutAt, now)
                .SetProperty(s => s.TotalHours, hours)
                .SetProperty(s => s.ShiftStatus, ShiftStatus.Completed));

        if (rows == 0)
        {
            TempData["Error"] = "This shift was already closed.";
            return RedirectToAction(nameof(Shifts));
        }

        await audit.LogAsync(uid, "ShiftClockOut", $"ShiftId={open.Id}; Hours={hours:0.00}");
        TempData["Success"] = $"Clocked out. Shift total: {hours:0.00} h.";
        return RedirectToAction(nameof(Shifts));
    }
}