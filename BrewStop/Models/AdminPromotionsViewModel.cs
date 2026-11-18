using System.ComponentModel.DataAnnotations;

namespace BrewStop.Models;

public class CreatePromotionInput
{
    [Required, StringLength(30, MinimumLength = 3)]
    [RegularExpression("^[A-Za-z0-9]+$", ErrorMessage = "Code may contain letters and digits only.")]
    public string Code { get; set; } = "";

    [Required, StringLength(100)]
    public string Title { get; set; } = "";

    [Required]
    public DiscountType DiscountType { get; set; } = DiscountType.Percentage;

    [Range(0.01, 100000)]
    public decimal DiscountValue { get; set; }

    [Required, DataType(DataType.Date)]
    public DateTime? ExpiryDate { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Max uses must be at least 1.")]
    public int? MaxUses { get; set; }
}

public class AdminPromotionsViewModel
{
    public List<Promotion> Promotions { get; set; } = new();
    public CreatePromotionInput Input { get; set; } = new();
}