using BrewStop.Data;
using BrewStop.Models;
using BrewStop.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BrewStop.Controllers;


public class MenuController(ApplicationDbContext db, IImageResolver images) : Controller
{
    private readonly ApplicationDbContext _db = db;
    private readonly IImageResolver _images = images; 

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


                ImageUrl = _images.Resolve(c.ImageUrl, c.Name),

                Items = c.MenuItems.Select(m => new MenuItemDto
                {
                    Id = m.Id,
                    Name = m.Name,
                    Description = m.Description,
                    Price = m.Price,


                    ImageUrl = _images.Resolve(m.ImageUrl, m.Name),

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