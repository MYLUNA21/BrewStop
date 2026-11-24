using System.Security.Claims;
using BrewStop.Data;
using BrewStop.Hubs;
using BrewStop.Models;
using BrewStop.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace BrewStop.Controllers.Barista;

[Authorize(Roles = "Barista,Admin")]
[Route("Barista")]
public class BaristaController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IHubContext<OrderHub> _hub;
    private readonly ICustomerNotifier _notifier;

    public BaristaController(
        ApplicationDbContext db,
        IHubContext<OrderHub> hub,
        ICustomerNotifier notifier)
    {
        _db = db;
        _hub = hub;
        _notifier = notifier;
    }

    public class BaristaQueueViewModel
    {
        public List<Order> PlacedOrders { get; set; } = new();
        public List<Order> PreparingOrders { get; set; } = new();
        public List<Order> ReadyOrders { get; set; } = new();
        public List<Order> RecentCompleted { get; set; } = new();
    }

    [HttpGet("")]
    [HttpGet("Index")]
    public async Task<IActionResult> Index()
    {
        var activeStatuses = new[] { OrderStatus.Placed, OrderStatus.Preparing, OrderStatus.Ready };

        var activeOrders = await _db.Orders
            .AsNoTracking()
            .Include(o => o.User)
            .Include(o => o.Items)
                .ThenInclude(i => i.AddOns)
            .Where(o => activeStatuses.Contains(o.Status))
            .OrderBy(o => o.PickupAtUtc)
            .ThenBy(o => o.CreatedAtUtc)
            .ToListAsync();

        var todayStartUtc = DateTime.UtcNow.Date;
        var recentCompleted = await _db.Orders
            .AsNoTracking()
            .Include(o => o.User)
            .Include(o => o.Items)
                .ThenInclude(i => i.AddOns)
            .Where(o => (o.Status == OrderStatus.Claimed || o.Status == OrderStatus.Cancelled)
                     && o.CreatedAtUtc >= todayStartUtc)
            .OrderByDescending(o => o.CreatedAtUtc)
            .Take(10)
            .ToListAsync();

        var vm = new BaristaQueueViewModel
        {
            PlacedOrders = activeOrders.Where(o => o.Status == OrderStatus.Placed).ToList(),
            PreparingOrders = activeOrders.Where(o => o.Status == OrderStatus.Preparing).ToList(),
            ReadyOrders = activeOrders.Where(o => o.Status == OrderStatus.Ready).ToList(),
            RecentCompleted = recentCompleted
        };

        return View("~/Views/Barista/Index.cshtml", vm);
    }

    [HttpPost("AdvanceStatus")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AdvanceStatus(int orderId, OrderStatus nextStatus)
    {
        var order = await _db.Orders.FirstOrDefaultAsync(o => o.Id == orderId);
        if (order == null)
        {
            TempData["Error"] = $"Order #{orderId} was not found.";
            return RedirectToAction(nameof(Index));
        }

        var validTransition = (order.Status, nextStatus) switch
        {
            (OrderStatus.Placed, OrderStatus.Preparing) => true,
            (OrderStatus.Preparing, OrderStatus.Ready) => true,
            (OrderStatus.Ready, OrderStatus.Claimed) => true,
            (OrderStatus.Placed, OrderStatus.Cancelled) => true,
            (OrderStatus.Preparing, OrderStatus.Cancelled) => true,
            _ => false
        };

        if (!validTransition)
        {
            TempData["Error"] = $"Cannot move Order #{order.Id} from {order.Status} to {nextStatus}.";
            return RedirectToAction(nameof(Index));
        }

        if (nextStatus == OrderStatus.Claimed && order.PaymentStatus != PaymentStatus.Paid)
        {
            TempData["Error"] = $"Order #{order.Id} must be marked as Paid before it can be claimed.";
            return RedirectToAction(nameof(Index));
        }

        var previous = order.Status;
        order.Status = nextStatus;

        _db.AuditLogs.Add(new AuditLog
        {
            UserId = User.FindFirstValue(ClaimTypes.NameIdentifier),
            Action = $"Order{nextStatus}",
            Details = $"Barista updated Order #{order.Id} from {previous} to {nextStatus}.",
            CreatedAtUtc = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();

        var payload = new
        {
            orderId = order.Id,
            status = order.Status.ToString(),
            paymentStatus = order.PaymentStatus.ToString()
        };

        // Notify all connected baristas/admins & the customer
        await _hub.Clients.Group("staff").SendAsync("OrderUpdated", payload);
        await _hub.Clients.Group($"user:{order.UserId}").SendAsync("OrderStatusChanged", payload);
        await _hub.Clients.Group($"user:{order.UserId}").SendAsync("UnreadCountChanged",
            new { unreadCount = await _notifier.UnreadCountAsync(order.UserId) });

        TempData["Success"] = $"Order #{order.Id} moved to {nextStatus}.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("MarkPaid")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkPaid(int orderId, string? paymentReference)
    {
        var order = await _db.Orders.FirstOrDefaultAsync(o => o.Id == orderId);
        if (order == null)
        {
            TempData["Error"] = $"Order #{orderId} was not found.";
            return RedirectToAction(nameof(Index));
        }

        if (order.PaymentStatus == PaymentStatus.Paid)
        {
            TempData["Success"] = $"Order #{order.Id} is already marked as Paid.";
            return RedirectToAction(nameof(Index));
        }

        order.PaymentStatus = PaymentStatus.Paid;
        order.PaidAtUtc = DateTime.UtcNow;
        if (!string.IsNullOrWhiteSpace(paymentReference))
        {
            order.PaymentReference = paymentReference.Trim();
        }

        _db.AuditLogs.Add(new AuditLog
        {
            UserId = User.FindFirstValue(ClaimTypes.NameIdentifier),
            Action = "OrderPaid",
            Details = $"Order #{order.Id} ({order.PaymentMethod}, ₱{order.Total:N2}) confirmed Paid" +
                      (string.IsNullOrWhiteSpace(order.PaymentReference) ? "." : $" [Ref: {order.PaymentReference}]."),
            CreatedAtUtc = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();

        var payload = new
        {
            orderId = order.Id,
            status = order.Status.ToString(),
            paymentStatus = order.PaymentStatus.ToString()
        };

        await _hub.Clients.Group("staff").SendAsync("OrderUpdated", payload);
        await _hub.Clients.Group($"user:{order.UserId}").SendAsync("OrderStatusChanged", payload);

        TempData["Success"] = $"Payment confirmed for Order #{order.Id}.";
        return RedirectToAction(nameof(Index));
    }
}