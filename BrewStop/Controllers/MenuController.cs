
using BrewStop.Data;
using BrewStop.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BrewStop.Controllers;

public class MenuController : Controller
{
    private readonly ApplicationDbContext _db;
    public MenuController(ApplicationDbContext db) => _db = db;

    public async Task<IActionResult> Index(string? category)
    {
        var categories = await _db.Categories
            .AsNoTracking()
            .Include(c => c.MenuItems)
            .OrderBy(c => c.Id)
            .ToListAsync();

            var stockDict = await _db.InventoryItems
            .AsNoTracking()
            .ToDictionaryAsync(i => i.MenuItemId, i => i.Quantity);

        var vm = new MenuViewModel
        {
            Categories = categories.Select(c => new CategoryGroupDto
            {
                Slug = c.Slug,
                Name = c.Name,
                Group = c.Group,
                ImageUrl = c.ImageUrl,
                Items = c.MenuItems.Select(m => new MenuItemDto
                {
                    Id = m.Id,
                    Name = m.Name,
                    Description = m.Description,
                    Price = m.Price,
                    ImageUrl = m.ImageUrl,
                    BadgeText = m.BadgeText,
                    CategorySlug = c.Slug,
                    CategoryName = c.Name,

                    Stock = stockDict.GetValueOrDefault(m.Id, 0)

                }).ToList()
            }).ToList()
        };

        ViewBag.ActiveCategory = category ?? "all";
        return View(vm);
    }
}