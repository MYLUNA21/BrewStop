using Microsoft.EntityFrameworkCore;

namespace BrewStop.Data;

public static class ReviewQueries
{
    public static async Task<Dictionary<int, (int Count, double Avg)>> GetRatingStatsAsync(
        this ApplicationDbContext db, List<int>? menuItemIds = null, CancellationToken ct = default)
    {
        var q = db.ProductReviews.AsNoTracking();
        if (menuItemIds is not null) q = q.Where(r => menuItemIds.Contains(r.MenuItemId));

        var rows = await q.GroupBy(r => r.MenuItemId)
            .Select(g => new { Id = g.Key, Count = g.Count(), Avg = g.Average(x => (double)x.Rating) })
            .ToListAsync(ct);

        return rows.ToDictionary(r => r.Id, r => (r.Count, r.Avg));
    }
}