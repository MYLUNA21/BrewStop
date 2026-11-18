using BrewStop.Data;
using BrewStop.Models;
using Microsoft.EntityFrameworkCore;

namespace BrewStop.Services;

public record PromoResult(bool Ok, string? Error, string? Code, decimal Discount, int? PromotionId = null);

public interface IPromotionService
{
    /// <summary>
    /// Validates the code server-side, computes the discount and atomically increments TimesUsed.
    /// Call inside the caller's transaction and roll back if the order fails afterwards.
    /// A blank code returns Ok with a zero discount.
    /// </summary>
    Task<PromoResult> ApplyPromotionAsync(string? code, decimal subtotal, CancellationToken ct = default);
}

public class PromotionService(ApplicationDbContext db) : IPromotionService
{
    private static PromoResult Fail(string msg) => new(false, msg, null, 0m);

    public async Task<PromoResult> ApplyPromotionAsync(string? code, decimal subtotal, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(code)) return new PromoResult(true, null, null, 0m);

        var normalized = code.Trim().ToUpperInvariant();
        var now = DateTime.UtcNow;

        var promo = await db.Promotions.AsNoTracking().FirstOrDefaultAsync(p => p.Code == normalized, ct);
        if (promo is null || !promo.IsActive) return Fail("Invalid promo code.");
        if (promo.ExpiryDate < now) return Fail("This promo code has expired.");
        if (promo.MaxUses is not null && promo.TimesUsed >= promo.MaxUses) return Fail("This promo code has reached its usage limit.");

        var discount = promo.DiscountType == DiscountType.Percentage
            ? Math.Round(subtotal * promo.DiscountValue / 100m, 2, MidpointRounding.AwayFromZero)
            : promo.DiscountValue;
        discount = Math.Min(discount, subtotal);

        // Conditional UPDATE: two buyers can never both take the last use.
        var rows = await db.Promotions
            .Where(p => p.Id == promo.Id && p.IsActive && p.ExpiryDate >= now
                        && (p.MaxUses == null || p.TimesUsed < p.MaxUses))
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.TimesUsed, p => p.TimesUsed + 1), ct);

        return rows == 0 ? Fail("This promo code is no longer available.")
                         : new PromoResult(true, null, promo.Code, discount, promo.Id);
    }
}