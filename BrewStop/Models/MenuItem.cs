using System.ComponentModel.DataAnnotations;

namespace BrewStop.Models;

public class MenuItem
{
    public int Id { get; set; }

    [Required, MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string Description { get; set; } = string.Empty;

    public decimal Price { get; set; }

    [MaxLength(500)]
    public string ImageUrl { get; set; } = string.Empty;

    [MaxLength(50)]
    public string BadgeText { get; set; } = "Bestseller";

    public int CategoryId { get; set; }
    public Category Category { get; set; } = null!;
}