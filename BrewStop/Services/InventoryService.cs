using BrewStop.Data;
using Microsoft.EntityFrameworkCore;

namespace BrewStop.Services;

public record StockResult(bool Ok, List<int> ShortItemIds, List<int> SoldOutItemIds);

public interface IInventoryService
{
    Task<Dictionary<int, int>> GetStockAsync(IEnumerable<int> menuItemIds, CancellationToken ct = default);

    /// <summary>
    /// Atomically deducts stock. Call inside the caller's transaction and roll back when Ok is false.
    /// </summary>
    Task<StockResult> DeductAsync(IReadOnlyDictionary<int, int> quantities, CancellationToken ct = default);
}

public class InventoryService(ApplicationDbContext db) : IInventoryService
{
    public async Task<Dictionary<int, int>> GetStockAsync(IEnumerable<int> menuItemIds, CancellationToken ct = default)
    {
        var ids = menuItemIds.Distinct().ToList();
        return await db.InventoryItems.AsNoTracking()
            .Where(i => ids.Contains(i.MenuItemId))
            .ToDictionaryAsync(i => i.MenuItemId, i => i.Quantity, ct);
    }

    public async Task<StockResult> DeductAsync(IReadOnlyDictionary<int, int> quantities, CancellationToken ct = default)
    {
        var shortIds = new List<int>();

        // Fixed lock order (by item id) prevents deadlocks between concurrent checkouts.
        foreach (var (itemId, qty) in quantities.OrderBy(k => k.Key))
        {
            // One conditional UPDATE: succeeds only if enough stock remains, so stock can never go negative
            // and two buyers can never both take the last item (SRS-NFR-09).
            var rows = await db.InventoryItems
                .Where(i => i.MenuItemId == itemId && i.Quantity >= qty)
                .ExecuteUpdateAsync(s => s.SetProperty(i => i.Quantity, i => i.Quantity - qty), ct);

            if (rows == 0)
            {
                shortIds.Add(itemId);
                return new StockResult(false, shortIds, new List<int>());
            }
        }

        var ids = quantities.Keys.ToList();
        var soldOut = await db.InventoryItems.AsNoTracking()
            .Where(i => ids.Contains(i.MenuItemId) && i.Quantity == 0)
            .Select(i => i.MenuItemId)
            .ToListAsync(ct);

        return new StockResult(true, shortIds, soldOut);
    }
}