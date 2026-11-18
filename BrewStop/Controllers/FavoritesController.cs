using System.Security.Claims;
using BrewStop.Data;
using BrewStop.Models;
using BrewStop.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BrewStop.Controllers;

[Authorize(Policy = "CustomerOnly")]
public class FavoritesController(ApplicationDbContext db, ICartService cart, IImageResolver images) : Controller
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    private bool IsAjax => Request.Headers.XRequestedWith == "XMLHttpRequest";

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var uid = UserId;
        var rows = await db.FavoriteItems.AsNoTracking()
            .Where(f => f.UserId == uid)
            .OrderByDescending(f => f.CreatedAtUtc)
            .Select(f => new
            {
                f.MenuItem,
                CategoryName = f.MenuItem.Category.Name,
                CategorySlug = f.MenuItem.Category.Slug
            })
            .ToListAsync();

        var ids = rows.Select(r => r.MenuItem.Id).ToList();
        var stock = await db.InventoryItems.AsNoTracking()
            .Where(i => ids.Contains(i.MenuItemId))
            .ToDictionaryAsync(i => i.MenuItemId, i => i.Quantity);
        var stats = await db.GetRatingStatsAsync(ids);

        var items = rows.Select(r =>
        {
            var s = stats.GetValueOrDefault(r.MenuItem.Id);
            return new MenuItemDto
            {
                Id = r.MenuItem.Id,
                Name = r.MenuItem.Name,
                Description = r.MenuItem.Description,
                Price = r.MenuItem.Price,
                ImageUrl = images.Resolve(r.MenuItem.ImageUrl, r.MenuItem.Name),
                BadgeText = r.MenuItem.BadgeText,
                CategorySlug = r.CategorySlug,
                CategoryName = r.CategoryName,
                Stock = stock.GetValueOrDefault(r.MenuItem.Id, 0),
                AverageRating = s.Avg,
                ReviewCount = s.Count,
                IsFavorite = true
            };
        }).ToList();

        return View(items);
    }

    [HttpPost]
    public async Task<IActionResult> Toggle(int menuItemId, string? returnUrl = null)
    {
        var uid = UserId;
        if (!await db.MenuItems.AnyAsync(m => m.Id == menuItemId))
            return NotFound();

        var existing = await db.FavoriteItems
            .FirstOrDefaultAsync(f => f.UserId == uid && f.MenuItemId == menuItemId);

        if (existing is null)
            db.FavoriteItems.Add(new FavoriteItem { UserId = uid, MenuItemId = menuItemId });
        else
            db.FavoriteItems.Remove(existing);

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException)   // unique-index race (double click): re-read the truth
        {
            db.ChangeTracker.Clear();
        }

        var saved = await db.FavoriteItems.AsNoTracking()
            .AnyAsync(f => f.UserId == uid && f.MenuItemId == menuItemId);

        if (IsAjax) return Json(new { saved, menuItemId });
        return LocalRedirect(Url.IsLocalUrl(returnUrl) ? returnUrl! : "/Favorites");
    }

    [HttpPost]
    public async Task<IActionResult> Remove(int menuItemId)
    {
        var uid = UserId;
        await db.FavoriteItems.Where(f => f.UserId == uid && f.MenuItemId == menuItemId).ExecuteDeleteAsync();
        TempData["FavSuccess"] = "Removed from favorites.";
        return RedirectToAction(nameof(Index));
    }

    // 1-click add: default (first active) size for drinks, no add-ons, qty 1. Stock rules enforced by CartService.
    [HttpPost]
    public async Task<IActionResult> QuickAdd(int menuItemId)
    {
        var item = await db.MenuItems.AsNoTracking()
            .Include(m => m.Category)
            .FirstOrDefaultAsync(m => m.Id == menuItemId);
        if (item is null) return NotFound();

        int? sizeId = null;
        if (item.Category.Group == "Drinks")
            sizeId = await db.ItemSizes.AsNoTracking()
                .Where(s => s.IsActive)
                .OrderBy(s => s.SortOrder)
                .Select(s => (int?)s.Id)
                .FirstOrDefaultAsync();

        var (ok, error) = await cart.AddAsync(menuItemId, sizeId, Array.Empty<int>(), 1);
        if (!ok)
        {
            TempData["FavError"] = error;
            return RedirectToAction(nameof(Index));
        }

        return RedirectToAction("Index", "Cart");
    }
}