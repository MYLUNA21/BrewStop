using System.ComponentModel.DataAnnotations;

namespace BrewStop.Models;

public enum NotificationType { OrderPreparing, OrderReady, OrderCompleted, OrderCancelled, Promotion }

public class CustomerNotification
{
    public int Id { get; set; }

    [Required, MaxLength(255)]
    public string UserId { get; set; } = string.Empty;

    public NotificationType Type { get; set; }

    [Required, MaxLength(120)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(300)]
    public string? Message { get; set; }

    public int? OrderId { get; set; }

    public bool IsRead { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ReadAtUtc { get; set; }
}