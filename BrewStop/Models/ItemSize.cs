using System.ComponentModel.DataAnnotations;

namespace BrewStop.Models;

public class ItemSize
{
    public int Id { get; set; }

    [Required, MaxLength(30)]
    public string Name { get; set; } = string.Empty;

    public decimal PriceDelta { get; set; }

    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}