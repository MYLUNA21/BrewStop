using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BrewStop.Models;

/// <summary>
/// A selectable option (size, milk, syrup...) that can be attached to many menu items.
/// </summary>
[Table("Customizations")]
[Index(nameof(Type), nameof(DisplayOrder))]
public class Customization
{
    [Key]
    public int Id { get; set; }

    [Required, StringLength(60, MinimumLength = 2)]
    public string Name { get; set; } = string.Empty;

    [Required]
    public CustomizationType Type { get; set; }

    /// <summary>Extra charge in PHP. Never negative.</summary>
    [Range(typeof(decimal), "0", "999.99")]
    [Column(TypeName = "decimal(10,2)")]
    [DataType(DataType.Currency)]
    public decimal PriceAdjustment { get; set; }

    /// <summary>Pre-selected in the UI for its group.</summary>
    public bool IsDefault { get; set; }

    public bool IsAvailable { get; set; } = true;

    [Range(0, int.MaxValue)]
    public int DisplayOrder { get; set; }

    /// <summary>Add-ons are multi-select; every other type is single-select within its group.</summary>
    [NotMapped]
    public bool AllowsMultiple => Type == CustomizationType.AddOn;

    // Navigation
    public ICollection<MenuItem> MenuItems { get; set; } = new List<MenuItem>();
    public ICollection<OrderItemCustomization> OrderItemCustomizations { get; set; } = new List<OrderItemCustomization>();
}
