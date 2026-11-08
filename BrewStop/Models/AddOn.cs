using System.ComponentModel.DataAnnotations;

namespace BrewStop.Models;

public class AddOn
{
    public int Id { get; set; }

    [Required, MaxLength(60)]
    public string Name { get; set; } = string.Empty;

    public decimal Price { get; set; }

    [Required, MaxLength(50)]
    public string Group { get; set; } = "Drinks";

    public bool IsActive { get; set; } = true;
}