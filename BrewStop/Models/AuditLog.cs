using System.ComponentModel.DataAnnotations;

namespace BrewStop.Models;

public class AuditLog
{
    public long Id { get; set; }

    /// <summary>Null for system actions. No foreign key, so history survives user deletion.</summary>
    [MaxLength(64)]
    public string? UserId { get; set; }

    [Required, MaxLength(60)]
    public string Action { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Details { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}