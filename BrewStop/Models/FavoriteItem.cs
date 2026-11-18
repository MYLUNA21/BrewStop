namespace BrewStop.Models;

public class FavoriteItem
{
    public int Id { get; set; }

    [System.ComponentModel.DataAnnotations.Required]
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;

    public int MenuItemId { get; set; }
    public MenuItem MenuItem { get; set; } = null!;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}