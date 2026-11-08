using System.ComponentModel.DataAnnotations;

namespace BrewStop.Models;

public enum OrderStatus { Placed, Preparing, Ready, Claimed, Cancelled }

public class Order
{
    public int Id { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;

    public OrderStatus Status { get; set; } = OrderStatus.Placed;

    public DateTime PickupAtUtc { get; set; }

    [RegularExpression(@"^[a-zA-Z0-9\s.,!?()'-]*$", ErrorMessage = "Note contains characters that are not allowed.")]
    [MaxLength(200)]
    public string? Note { get; set; }

    public decimal Total { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public List<OrderItem> Items { get; set; } = new();
}

public class OrderItem
{
    public int Id { get; set; }

    public int OrderId { get; set; }
    public Order Order { get; set; } = null!;

    public int MenuItemId { get; set; }
    public MenuItem MenuItem { get; set; } = null!;

    [Required, MaxLength(150)]
    public string ItemName { get; set; } = string.Empty;

    [MaxLength(30)]
    public string? SizeName { get; set; }

    public decimal UnitPrice { get; set; }

    public int Quantity { get; set; }

    public List<OrderItemAddOn> AddOns { get; set; } = new();
}

public class OrderItemAddOn
{
    public int Id { get; set; }

    public int OrderItemId { get; set; }
    public OrderItem OrderItem { get; set; } = null!;

    public int AddOnId { get; set; }
    public AddOn AddOn { get; set; } = null!;

    [Required, MaxLength(60)]
    public string Name { get; set; } = string.Empty;

    public decimal Price { get; set; }
}