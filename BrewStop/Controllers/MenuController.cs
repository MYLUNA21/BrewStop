using System.Security.Claims;
using BrewStop.Data;
using BrewStop.Models;
using BrewStop.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BrewStop.Controllers;

public class MenuController(ApplicationDbContext db, IImageResolver images) : Controller
{
    public async Task<IActionResult> Index(string? category, string? searchTerm)
    {
        if (User.IsInRole(AppRoles.Admin))
            return RedirectToAction("Dashboard", "Admin");

        var term = searchTerm?.Trim();
        if (term is { Length: > 100 }) term = term[..100];

        var categories = await db.Categories
            .AsNoTracking()
            .Include(c => c.MenuItems)
            .OrderBy(c => c.Id)
            .ToListAsync();

        var stock = await db.InventoryItems
            .AsNoTracking()
            .ToDictionaryAsync(i => i.MenuItemId, i => i.Quantity);

        var uid = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var isCustomer = User.IsInRole(AppRoles.Student) || User.IsInRole(AppRoles.Faculty);
        var stats = await db.GetRatingStatsAsync();
        var favs = isCustomer && uid is not null
            ? (await db.FavoriteItems.AsNoTracking().Where(f => f.UserId == uid)
                  .Select(f => f.MenuItemId).ToListAsync()).ToHashSet()
            : new HashSet<int>();

        var all = categories.Select(c => new CategoryGroupDto
        {
            Slug = c.Slug,
            Name = c.Name,
            Group = c.Group,
            ImageUrl = images.Resolve(c.ImageUrl, c.Name),
            Items = c.MenuItems.Select(m => new MenuItemDto
            {
                Id = m.Id,
                Name = m.Name,
                Description = m.Description,
                Price = m.Price,
                ImageUrl = images.Resolve(m.ImageUrl, m.Name),
                BadgeText = m.BadgeText,
                CategorySlug = c.Slug,
                CategoryName = c.Name,
                Stock = stock.GetValueOrDefault(m.Id, 0),
                AverageRating = stats.GetValueOrDefault(m.Id).Avg,
                ReviewCount = stats.GetValueOrDefault(m.Id).Count,
                IsFavorite = favs.Contains(m.Id)
            }).ToList()
        }).ToList();

        var active = string.IsNullOrWhiteSpace(category) ? "all" : category.Trim();
        if (!active.Equals("all", StringComparison.OrdinalIgnoreCase) &&
            !all.Any(c => c.Slug.Equals(active, StringComparison.OrdinalIgnoreCase)))
            active = "all";

        var visible = all
            .Where(c => active.Equals("all", StringComparison.OrdinalIgnoreCase) ||
                        c.Slug.Equals(active, StringComparison.OrdinalIgnoreCase))
            .Select(c => new CategoryGroupDto
            {
                Slug = c.Slug,
                Name = c.Name,
                Group = c.Group,
                ImageUrl = c.ImageUrl,
                Items = string.IsNullOrEmpty(term)
                    ? c.Items
                    : c.Items.Where(i =>
                        i.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                        i.Description.Contains(term, StringComparison.OrdinalIgnoreCase)).ToList()
            })
            .Where(c => c.Items.Count > 0)
            .ToList();

        await LoadStaffReferenceAsync();

        return View(new MenuViewModel
        {
            Categories = visible,
            Pills = all,
            ActiveCategory = active.ToLowerInvariant(),
            SearchTerm = term
        });
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var m = await db.MenuItems.AsNoTracking()
            .Include(x => x.Category)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (m is null) return NotFound();

        var stock = await db.InventoryItems.AsNoTracking()
            .Where(i => i.MenuItemId == id)
            .Select(i => i.Quantity)
            .FirstOrDefaultAsync();

        var ratings = await db.ProductReviews.AsNoTracking()
            .Where(r => r.MenuItemId == id)
            .Select(r => r.Rating)
            .ToListAsync();

        var recent = await db.ProductReviews.AsNoTracking()
            .Where(r => r.MenuItemId == id)
            .OrderByDescending(r => r.CreatedAtUtc)
            .Take(10)
            .Select(r => new { r.User.FullName, r.Rating, r.Comment, r.CreatedAtUtc })
            .ToListAsync();

        var uid = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var isCustomer = User.IsInRole(AppRoles.Student) || User.IsInRole(AppRoles.Faculty);
        var isFav = isCustomer && uid is not null &&
                    await db.FavoriteItems.AsNoTracking().AnyAsync(f => f.UserId == uid && f.MenuItemId == id);

        var dist = new int[5];
        foreach (var r in ratings) dist[Math.Clamp(r, 1, 5) - 1]++;

        await LoadStaffReferenceAsync();
        ViewBag.RefGroup = m.Category.Group;

        return View(new MenuItemDetailsViewModel
        {
            CanFavorite = isCustomer,
            Distribution = dist,
            Item = new MenuItemDto
            {
                Id = m.Id,
                Name = m.Name,
                Description = m.Description,
                Price = m.Price,
                ImageUrl = images.Resolve(m.ImageUrl, m.Name),
                BadgeText = m.BadgeText,
                CategorySlug = m.Category.Slug,
                CategoryName = m.Category.Name,
                Stock = stock,
                AverageRating = ratings.Count > 0 ? ratings.Average() : 0,
                ReviewCount = ratings.Count,
                IsFavorite = isFav
            },
            Reviews = recent.Select(r => new ReviewDto
            {
                Author = DisplayName(r.FullName),
                Rating = r.Rating,
                Comment = r.Comment,
                CreatedAtUtc = r.CreatedAtUtc
            }).ToList()
        });
    }

    private async Task LoadStaffReferenceAsync()
    {
        if (!User.IsInRole(AppRoles.Barista)) return;
        ViewBag.RefSizes = await db.ItemSizes.AsNoTracking()
            .Where(s => s.IsActive).OrderBy(s => s.SortOrder).ToListAsync();
        ViewBag.RefAddOns = await db.AddOns.AsNoTracking()
            .Where(a => a.IsActive).OrderBy(a => a.Name).ToListAsync();
    }

    private static string DisplayName(string? full)
    {
        var p = (full ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return p.Length switch { 0 => "Customer", 1 => p[0], _ => $"{p[0]} {p[^1][0]}." };
    }
}