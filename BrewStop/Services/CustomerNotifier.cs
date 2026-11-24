using System.Net;
using BrewStop.Data;
using BrewStop.Hubs;
using BrewStop.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace BrewStop.Services;

public interface ICustomerNotifier
{
    Task<int> UnreadCountAsync(string userId, CancellationToken ct = default);
    Task MarkAllReadAsync(string userId, CancellationToken ct = default);
    Task OrderStatusAsync(int orderId, string userId, OrderStatus status, CancellationToken ct = default);
    Task PromotionAsync(Promotion promo, CancellationToken ct = default);
}

public class CustomerNotifier(ApplicationDbContext db, IHubContext<OrderHub> hub, IEmailService mailer) : ICustomerNotifier
{
    public Task<int> UnreadCountAsync(string userId, CancellationToken ct = default) =>
        db.CustomerNotifications.AsNoTracking().CountAsync(n => n.UserId == userId && !n.IsRead, ct);

    public async Task MarkAllReadAsync(string userId, CancellationToken ct = default)
    {
        await db.CustomerNotifications.Where(n => n.UserId == userId && !n.IsRead)
            .ExecuteUpdateAsync(s => s
                .SetProperty(n => n.IsRead, true)
                .SetProperty(n => n.ReadAtUtc, (DateTime?)DateTime.UtcNow), ct);

        await hub.Clients.Group($"user:{userId}")
            .SendAsync("UnreadCountChanged", new { unreadCount = 0 }, ct);
    }

    public async Task OrderStatusAsync(int orderId, string userId, OrderStatus status, CancellationToken ct = default)
    {
        var d = Describe(orderId, status);
        if (d is null) return;

        if (status == OrderStatus.Ready)
        {
            var wantsReady = await db.Users.AsNoTracking()
                .Where(u => u.Id == userId).Select(u => u.NotifyOrderReady).FirstOrDefaultAsync(ct);
            if (!wantsReady) return;
        }

        var note = new CustomerNotification
        {
            UserId = userId,
            Type = d.Value.Type,
            Title = d.Value.Title,
            Message = d.Value.Message,
            OrderId = orderId
        };
        db.CustomerNotifications.Add(note);
        await db.SaveChangesAsync(ct);

        await PushAsync(note, await UnreadCountAsync(userId, ct), ct);
    }

    public async Task PromotionAsync(Promotion promo, CancellationToken ct = default)
    {
        var q = db.Users.AsNoTracking().Where(u => !u.IsDeleted && u.NotifyPromotions);
        if (promo.OwnerUserId is not null) q = q.Where(u => u.Id == promo.OwnerUserId);

        var recipients = await q.Select(u => new { u.Id, u.Email }).ToListAsync(ct);
        if (recipients.Count == 0) return;
        var ids = recipients.Select(r => r.Id).ToList();

        var off = promo.DiscountType == DiscountType.Percentage
            ? $"{promo.DiscountValue:0.##}% off"
            : $"\u20B1{promo.DiscountValue:0.##} off";

        var notes = ids.Select(id => new CustomerNotification
        {
            UserId = id,
            Type = NotificationType.Promotion,
            Title = promo.Title,
            Message = $"Use code {promo.Code} for {off}."
        }).ToList();

        db.CustomerNotifications.AddRange(notes);
        await db.SaveChangesAsync(ct);

        var counts = await db.CustomerNotifications.AsNoTracking()
            .Where(n => !n.IsRead && ids.Contains(n.UserId))
            .GroupBy(n => n.UserId)
            .Select(g => new { UserId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.UserId, x => x.Count, ct);

        foreach (var n in notes)
            await PushAsync(n, counts.GetValueOrDefault(n.UserId), ct);

        var subject = $"BrewStop promo: {promo.Title}";
        var body = $"<p><b>{WebUtility.HtmlEncode(promo.Title)}</b></p>"
                 + $"<p>Use code <b>{WebUtility.HtmlEncode(promo.Code)}</b> for {off}.</p>";
        var emails = recipients.Where(r => !string.IsNullOrWhiteSpace(r.Email)).Select(r => r.Email!).ToList();

        _ = Task.Run(async () =>
        {
            foreach (var to in emails) await mailer.SendAsync(to, subject, body);
        });
    }

    private Task PushAsync(CustomerNotification n, int unreadCount, CancellationToken ct) =>
        hub.Clients.Group($"user:{n.UserId}").SendAsync("NotificationReceived", new
        {
            id = n.Id,
            type = n.Type.ToString(),
            title = n.Title,
            message = n.Message,
            orderId = n.OrderId,
            createdAtUtc = n.CreatedAtUtc,
            isRead = false,
            unreadCount
        }, ct);

    private static (NotificationType Type, string Title, string Message)? Describe(int id, OrderStatus s) => s switch
    {
        OrderStatus.Preparing => (NotificationType.OrderPreparing, "Order is being prepared", $"Order #{id} is now being prepared."),
        OrderStatus.Ready => (NotificationType.OrderReady, "Ready for pickup", $"Order #{id} is ready for pickup."),
        OrderStatus.Claimed => (NotificationType.OrderCompleted, "Order completed", $"Order #{id} was picked up. Enjoy!"),
        OrderStatus.Cancelled => (NotificationType.OrderCancelled, "Order cancelled", $"Order #{id} was cancelled."),
        _ => null
    };
}