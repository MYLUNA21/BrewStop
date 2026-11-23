using System.Security.Claims;
using BrewStop.Data;
using BrewStop.Models;
using BrewStop.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BrewStop.Controllers;

public class MenuController(ApplicationDbContext db, IImageResolver images) : Controller
{
    private static readonly string[] PriceProps =
        { "PriceModifier", "PriceDelta", "PriceAdjustment", "ExtraPrice", "AdditionalPrice", "ExtraCost", "Price" };

    private static readonly string[] SortKeys = { "popularity", "price_asc", "price_desc" };

    public async Task<IActionResult> Index(string? category, string? searchTerm, string? group, string? sort)
    {
        if (User.IsInRole(AppRoles.Admin))
            return RedirectToAction("Dashboard", "Admin");

        var term = searchTerm?.Trim();
        if (term is { Length: > 100 }) term = term[..100];

        var sortKey = SortKeys.Contains(sort?.Trim().ToLowerInvariant()) ? sort!.Trim().ToLowerInvariant() : "popularity";

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
            Items = c.MenuItems.Select(m =>
            {
                var s = stats.GetValueOrDefault(m.Id);
                return new MenuItemDto
                {
                    Id = m.Id,
                    Name = m.Name,
                    Description = m.Description,
                    Price = m.Price,
                    ImageUrl = images.Resolve(m.ImageUrl, m.Name),
                    BadgeText = m.BadgeText,
                    CategorySlug = c.Slug,
                    CategoryName = c.Name,
                    Group = c.Group,
                    Stock = stock.GetValueOrDefault(m.Id, 0),
                    AverageRating = s.Avg,
                    ReviewCount = s.Count,
                    Popularity = s.Count + s.Avg / 10d,
                    IsFavorite = favs.Contains(m.Id)
                };
            }).ToList()
        }).ToList();

        var flat = all.SelectMany(c => c.Items).ToList();

        var activeGroup = "Drinks";
        if (!string.IsNullOrWhiteSpace(group))
        {
            activeGroup = group.Trim().Equals("Drinks", StringComparison.OrdinalIgnoreCase) ? "Drinks" : "Pastries";
        }
        else if (!string.IsNullOrWhiteSpace(category))
        {
            var match = all.FirstOrDefault(c => c.Slug.Equals(category.Trim(), StringComparison.OrdinalIgnoreCase));
            if (match is not null && !IsDrink(match.Group)) activeGroup = "Pastries";
        }

        IEnumerable<MenuItemDto> Filter(IEnumerable<MenuItemDto> src) =>
            string.IsNullOrEmpty(term)
                ? src
                : src.Where(i =>
                    i.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    i.Description.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    i.CategoryName.Contains(term, StringComparison.OrdinalIgnoreCase));

        List<MenuItemDto> Sort(IEnumerable<MenuItemDto> src) => (sortKey switch
        {
            "price_asc" => src.OrderBy(i => i.Price).ThenBy(i => i.Name),
            "price_desc" => src.OrderByDescending(i => i.Price).ThenBy(i => i.Name),
            _ => src.OrderByDescending(i => i.IsAvailable)
                    .ThenByDescending(i => i.Popularity)
                    .ThenBy(i => i.Name)
        }).ToList();

        var drinksAll = flat.Where(i => IsDrink(i.Group)).ToList();
        var pastriesAll = flat.Where(i => !IsDrink(i.Group)).ToList();

        var pairPool = drinksAll.OrderByDescending(d => d.Popularity).Select(d => d.Name).Take(6).ToList();
        for (var i = 0; i < pastriesAll.Count; i++)
            pastriesAll[i].PairingNote = pairPool.Count > 0 ? pairPool[i % pairPool.Count] : string.Empty;

        var drinks = Sort(Filter(drinksAll));
        var pastries = Sort(Filter(pastriesAll));

        var hero = drinksAll.Where(d => !string.IsNullOrWhiteSpace(d.ImageUrl))
                            .OrderByDescending(d => d.Popularity)
                            .FirstOrDefault()
                   ?? flat.FirstOrDefault(d => !string.IsNullOrWhiteSpace(d.ImageUrl));

        var sizes = await db.ItemSizes.AsNoTracking()
            .Where(s => s.IsActive).OrderBy(s => s.SortOrder).ToListAsync();
        var addOns = await db.AddOns.AsNoTracking()
            .Where(a => a.IsActive).OrderBy(a => a.Name).ToListAsync();

        await LoadStaffReferenceAsync();

        var visibleCats = all
            .Select(c => new CategoryGroupDto
            {
                Slug = c.Slug,
                Name = c.Name,
                Group = c.Group,
                ImageUrl = c.ImageUrl,
                Items = Sort(Filter(c.Items))
            })
            .Where(c => c.Items.Count > 0)
            .ToList();

        return View(new MenuViewModel
        {
            Categories = visibleCats,
            Pills = all,
            ActiveCategory = activeGroup.ToLowerInvariant(),
            ActiveGroup = activeGroup,
            Sort = sortKey,
            SearchTerm = term,
            Drinks = drinks,
            Pastries = pastries,
            HeroImageUrl = hero?.ImageUrl ?? string.Empty,
            HeroImageAlt = hero?.Name ?? "BrewStop coffee",
            Sizes = sizes.Select(s => new SizeOptionDto { Id = s.Id, Name = s.Name, Price = PriceOf(s) }).ToList(),
            AddOns = addOns.Select(a => new AddOnOptionDto { Id = a.Id, Name = a.Name, Price = PriceOf(a) }).ToList()
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
                Group = m.Category.Group,
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

    private static bool IsDrink(string? group) =>
        string.Equals(group?.Trim(), "Drinks", StringComparison.OrdinalIgnoreCase);

    private static decimal PriceOf(object entity)
    {
        var type = entity.GetType();
        foreach (var name in PriceProps)
        {
            var p = type.GetProperty(name);
            if (p is null) continue;
            switch (p.GetValue(entity))
            {
                case decimal d: return d;
                case double db: return (decimal)db;
                case float f: return (decimal)f;
                case int i: return i;
            }
        }
        return 0m;
    }

    private static string DisplayName(string? full)
    {
        var p = (full ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return p.Length switch { 0 => "Customer", 1 => p[0], _ => $"{p[0]} {p[^1][0]}." };
    }
}