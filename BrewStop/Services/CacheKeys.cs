using BrewStop.Data;
using BrewStop.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace BrewStop.Services;

public static class CacheKeys
{
    public const string MenuCatalog = "menu:catalog";
    public const string ItemSizes = "menu:item-sizes";
    public const string AddOns = "menu:add-ons";
    public const string ActivePromos = "promos:active";
}

public static class CacheOptions
{
    public static readonly MemoryCacheEntryOptions Catalog = new()
    {
        SlidingExpiration = TimeSpan.FromMinutes(30),
        AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(2)
    };

    // Short TTL: TimesUsed changes on every redemption and is not invalidated.
    public static readonly MemoryCacheEntryOptions Promos = new()
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
    };
}

public static class PromoCacheExtensions
{
    /// <summary>Read-only preview lookup. Never use for redemption; PromotionService.ApplyPromotionAsync stays on the DB.</summary>
    public static async Task<Promotion?> FindActivePromoAsync(
        this IMemoryCache cache, ApplicationDbContext db, string normalizedCode, CancellationToken ct = default)
    {
        var map = await cache.GetOrCreateAsync(CacheKeys.ActivePromos, async e =>
        {
            e.SetOptions(CacheOptions.Promos);
            var list = await db.Promotions.AsNoTracking().Where(p => p.IsActive).ToListAsync(ct);
            var d = new Dictionary<string, Promotion>(StringComparer.OrdinalIgnoreCase);
            foreach (var p in list) d.TryAdd(p.Code, p);
            return d;
        });
        return map!.GetValueOrDefault(normalizedCode);
    }
}