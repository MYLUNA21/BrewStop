using System.ComponentModel.DataAnnotations;

namespace BrewStop.Models;

public enum DiscountType { Percentage, FixedAmount }

public class Promotion
{
    public int Id { get; set; }

    [Required, MaxLength(30)]
    public string Code { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string Title { get; set; } = string.Empty;

    public DiscountType DiscountType { get; set; }
    public decimal DiscountValue { get; set; }
    public DateTime ExpiryDate { get; set; }
    public int? MaxUses { get; set; }
    public int TimesUsed { get; set; }
    public bool IsActive { get; set; } = true;

    /// <summary>When set, only this user may apply the code (loyalty reward vouchers).</summary>
    [MaxLength(255)]
    public string? OwnerUserId { get; set; }
}