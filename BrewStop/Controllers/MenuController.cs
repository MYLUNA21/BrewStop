using BrewStop.Data;
using BrewStop.Models;
using BrewStop.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BrewStop.Controllers;

public class MenuController(ApplicationDbContext db, IImageResolver images) : Controller
{
    public async Task<IActionResult> Index(string? category)
    {
        var categories = await db.Categories
            .AsNoTracking()
            .Include(c => c.MenuItems)
            .OrderBy(c => c.Id)
            .ToListAsync();

        var stock = await db.InventoryItems
            .AsNoTracking()
            .ToDictionaryAsync(i => i.MenuItemId, i => i.Quantity);

        var vm = new MenuViewModel
        {
            Categories = categories.Select(c => new CategoryGroupDto
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
                    Stock = stock.GetValueOrDefault(m.Id, 0)
                }).ToList()
            }).ToList()
        };

        ViewBag.ActiveCategory = category ?? "all";
        return View(vm);
    }
}