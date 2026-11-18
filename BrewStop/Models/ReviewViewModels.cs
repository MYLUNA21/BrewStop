using System.ComponentModel.DataAnnotations;

namespace BrewStop.Models;

public class ReviewInput
{
    public int OrderId { get; set; }
    public int MenuItemId { get; set; }

    [Range(1, 5, ErrorMessage = "Please choose a rating from 1 to 5.")]
    public int Rating { get; set; }

    [StringLength(300, ErrorMessage = "Comment must be 300 characters or fewer.")]
    [RegularExpression(@"^[^<>\u0000-\u0008\u000B\u000C\u000E-\u001F\u007F]*$",
        ErrorMessage = "Comment contains characters that are not allowed.")]
    public string? Comment { get; set; }
}

public class ReviewDto
{
    public string Author { get; set; } = string.Empty;
    public int Rating { get; set; }
    public string? Comment { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

public class MenuItemDetailsViewModel
{
    public MenuItemDto Item { get; set; } = null!;
    public List<ReviewDto> Reviews { get; set; } = new();
    public int[] Distribution { get; set; } = new int[5];   // index 0 = 1 star
    public bool CanFavorite { get; set; }
}