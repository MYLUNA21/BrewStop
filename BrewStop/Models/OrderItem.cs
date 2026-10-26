using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BrewStop.Models;

[Table("OrderItems")]
[Index(nameof(OrderId))]
[Index(nameof(MenuItemId))]
public class OrderItem
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int OrderId { get; set; }

    [Required]
    public int MenuItemId { get; set; }

    /// <summary>Snapshot of the item name so history survives menu renames.</summary>
    [Required, StringLength(100)]
    public string ItemName { get; set; } = string.Empty;

    [Range(1, 20, ErrorMessage = "Quantity must be between 1 and 20.")]
    public int Quantity { get; set; } = 1;

    /// <summary>Snapshot of the menu base price at order time (excludes customizations).</summary>
    [Range(typeof(decimal), "0", "9999.99")]
    [Column(TypeName = "decimal(10,2)")]
    [DataType(DataType.Currency)]
    public decimal UnitPrice { get; set; }

    [StringLength(200)]
    public string? SpecialInstructions { get; set; }

    // Navigation
    [ForeignKey(nameof(OrderId))]
    public Order Order { get; set; } = null!;

    [ForeignKey(nameof(MenuItemId))]
    public MenuItem MenuItem { get; set; } = null!;

    public ICollection<OrderItemCustomization> SelectedCustomizations { get; set; } = new List<OrderItemCustomization>();

    /// <summary>Per-unit price including selected customizations.</summary>
    [NotMapped]
    public decimal UnitPriceWithCustomizations =>
        UnitPrice + SelectedCustomizations.Sum(c => c.PriceAdjustment);

    [NotMapped]
    public decimal LineTotal => UnitPriceWithCustomizations * Quantity;
}
