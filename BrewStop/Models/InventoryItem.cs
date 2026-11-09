using System.ComponentModel.DataAnnotations;

namespace BrewStop.Models;

public class InventoryItem
{
    public int Id { get; set; }

    public int MenuItemId { get; set; }
    public MenuItem MenuItem { get; set; } = null!;

    [Range(0, int.MaxValue)]
    public int Quantity { get; set; }
}