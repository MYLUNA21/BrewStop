using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BrewStop.Models;

/// <summary>
/// A customization chosen on an order line. Name and price are snapshotted
/// so later menu edits never alter past orders. Composite key: (OrderItemId, CustomizationId).
/// </summary>
[Table("OrderItemCustomizations")]
public class OrderItemCustomization
{
    [Required]
    public int OrderItemId { get; set; }

    [Required]
    public int CustomizationId { get; set; }

    [Required, StringLength(60)]
    public string Name { get; set; } = string.Empty;

    [Range(typeof(decimal), "0", "999.99")]
    [Column(TypeName = "decimal(10,2)")]
    public decimal PriceAdjustment { get; set; }

    // Navigation
    [ForeignKey(nameof(OrderItemId))]
    public OrderItem OrderItem { get; set; } = null!;

    [ForeignKey(nameof(CustomizationId))]
    public Customization Customization { get; set; } = null!;
}
