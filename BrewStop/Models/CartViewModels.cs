using System.ComponentModel.DataAnnotations;

namespace BrewStop.Models;

public record CartAddOnDto(int Id, string Name, decimal Price);

public class CartLineViewModel
{
    public string Key { get; set; } = string.Empty;
    public int MenuItemId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public int? SizeId { get; set; }
    public string? SizeName { get; set; }
    public List<CartAddOnDto> AddOns { get; set; } = new();
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
    public decimal LineTotal => UnitPrice * Quantity;
}

public class CartViewModel
{
    public List<CartLineViewModel> Lines { get; set; } = new();
    public decimal Total => Lines.Sum(l => l.LineTotal);
    public int ItemCount => Lines.Sum(l => l.Quantity);
}

public class CustomizeViewModel
{
    public MenuItem Item { get; set; } = null!;
    public bool IsDrink { get; set; }
    public List<ItemSize> Sizes { get; set; } = new();
    public List<AddOn> AddOns { get; set; } = new();
    public int? DefaultSizeId { get; set; }
}

public class AddToCartInput
{
    public int MenuItemId { get; set; }
    public int? SizeId { get; set; }
    public List<int> AddOnIds { get; set; } = new();

    [Range(1, 10)]
    public int Quantity { get; set; } = 1;
}

public class CheckoutInput
{
    [Required(ErrorMessage = "Please choose a pickup time.")]
    [Display(Name = "Pickup time")]
    public string? PickupSlot { get; set; }

    [StringLength(200, ErrorMessage = "Note must be 200 characters or fewer.")]
    [RegularExpression(@"^[^<>\u0000-\u0008\u000B\u000C\u000E-\u001F\u007F]*$",
        ErrorMessage = "Note contains characters that are not allowed.")]
    public string? Note { get; set; }
}

public class CheckoutViewModel
{
    public CartViewModel Cart { get; set; } = new();
    public IReadOnlyList<Services.PickupSlot> Slots { get; set; } = Array.Empty<Services.PickupSlot>();
    public CheckoutInput Input { get; set; } = new();
}