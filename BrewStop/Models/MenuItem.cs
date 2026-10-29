using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BrewStop.Models;

[Table("MenuItems")]
[Index(nameof(Name), IsUnique = true)]
[Index(nameof(Category), nameof(IsAvailable))]
public class MenuItem : IAuditable
{
    [Key]
    public int Id { get; set; }

    [Required, StringLength(100, MinimumLength = 2)]
    [Display(Name = "Item name")]
    public string Name { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Description { get; set; }

    [Required]
    public ItemCategory Category { get; set; }

    /// <summary>Price in PHP before any customization add-ons.</summary>
    [Range(typeof(decimal), "0.01", "9999.99", ErrorMessage = "Price must be between 0.01 and 9999.99.")]
    [Column(TypeName = "decimal(10,2)")]
    [DataType(DataType.Currency)]
    public decimal BasePrice { get; set; }

    [StringLength(300)]
    public string? ImageUrl { get; set; }

    public bool IsAvailable { get; set; } = true;

    /// <summary>Highlighted in the larger Bento tiles on the storefront.</summary>
    public bool IsFeatured { get; set; }

    [Range(1, 120)]
    [Display(Name = "Prep time (minutes)")]
    public int PrepTimeMinutes { get; set; } = 5;

    [Range(0, int.MaxValue)]
    public int DisplayOrder { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation
    /// <summary>Customization options offered for this item (many-to-many).</summary>
    public ICollection<Customization> Customizations { get; set; } = new List<Customization>();
    public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
}
