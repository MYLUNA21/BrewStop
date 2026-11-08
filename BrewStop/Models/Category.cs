using System.ComponentModel.DataAnnotations;

namespace BrewStop.Models;

public class Category
{
    public int Id { get; set; }

    [Required, MaxLength(100)]
    public string Slug { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required, MaxLength(50)]
    public string Group { get; set; } = "Drinks";

    [MaxLength(500)]
    public string ImageUrl { get; set; } = string.Empty;

    public ICollection<MenuItem> MenuItems { get; set; } = new List<MenuItem>();
}