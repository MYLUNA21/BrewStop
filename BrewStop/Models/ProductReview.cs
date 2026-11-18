using System.ComponentModel.DataAnnotations;

namespace BrewStop.Models;

public class ProductReview
{
    public int Id { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;

    public int MenuItemId { get; set; }
    public MenuItem MenuItem { get; set; } = null!;

    public int OrderId { get; set; }
    public Order Order { get; set; } = null!;

    [Range(1, 5)]
    public int Rating { get; set; }

    /// <summary>Stored sanitized; always HTML-encoded on output by Razor (SRS-NFR-07).</summary>
    [MaxLength(300)]
    public string? Comment { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}