using System.ComponentModel.DataAnnotations;

namespace BrewStop.Models;

public class LoyaltyTransaction
{
    public int Id { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;

    public int? OrderId { get; set; }
    public Order? Order { get; set; }

    /// <summary>Positive = earned, negative = redeemed.</summary>
    public int PointsDelta { get; set; }

    [Required, MaxLength(120)]
    public string Reason { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public record RewardTier(string Key, string Title, string Description, int Cost, DiscountType Type, decimal Value);

public static class LoyaltyReasons
{
    public const string OrderPickup = "Order pickup";
}

public static class LoyaltyTiers
{
    public const decimal PesosPerPoint = 10m;   // 1 point per PHP 10 spent
    public const int VoucherValidDays = 30;

    public static readonly IReadOnlyList<RewardTier> All = new[]
    {
        new RewardTier("free-addon", "Free Add-On",
            "One add-on on us: up to \u20B125 off your order.", 50, DiscountType.FixedAmount, 25m),
        new RewardTier("drink-50", "\u20B150 Drink Voucher",
            "\u20B150 off your order.", 100, DiscountType.FixedAmount, 50m)
    };
}

public class RewardsViewModel
{
    public int Points { get; set; }
    public List<LoyaltyTransaction> History { get; set; } = new();
    public List<Promotion> Vouchers { get; set; } = new();
}