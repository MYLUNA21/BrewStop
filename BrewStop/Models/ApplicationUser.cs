using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace BrewStop.Models;

public class ApplicationUser : IdentityUser
{
    [Required, MaxLength(150)]
    public string FullName { get; set; } = string.Empty;

    [Required, MaxLength(30)]
    public string CampusId { get; set; } = string.Empty;

    public int LoyaltyPoints { get; set; }

    [MaxLength(300)]
    public string? AvatarUrl { get; set; }

    public bool NotifyOrderReady { get; set; } = true;
    public bool NotifyPromotions { get; set; } = false;

    public bool IsDeleted { get; set; } = false;
    public DateTime? DeletedAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}