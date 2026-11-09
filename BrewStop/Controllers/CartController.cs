using BrewStop.Data;
using BrewStop.Models;
using BrewStop.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BrewStop.Controllers;

public class CartController(ICartService cart, IInventoryService inventory, ApplicationDbContext db) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index() => View(await cart.GetAsync());

    [HttpGet]
    public async Task<IActionResult> Customize(int id)
    {
        var item = await db.MenuItems.AsNoTracking()
            .Include(m => m.Category)
            .FirstOrDefaultAsync(m => m.Id == id);
        if (item is null) return NotFound();

        var stockLeft = (await inventory.GetStockAsync(new[] { id })).GetValueOrDefault(id);
        if (stockLeft <= 0)
        {
            TempData["MenuError"] = $"{item.Name} is sold out.";
            return RedirectToAction("Index", "Menu");
        }

        var isDrink = item.Category.Group == "Drinks";
        var group = item.Category.Group;

        var sizes = isDrink
            ? await db.ItemSizes.AsNoTracking().Where(s => s.IsActive).OrderBy(s => s.SortOrder).ToListAsync()
            : new List<ItemSize>();
        var addOns = await db.AddOns.AsNoTracking()
            .Where(a => a.IsActive && a.Group == group)
            .OrderBy(a => a.Name)
            .ToListAsync();

        return View(new CustomizeViewModel
        {
            Item = item,
            IsDrink = isDrink,
            Sizes = sizes,
            AddOns = addOns,
            DefaultSizeId = sizes.FirstOrDefault()?.Id
        });
    }

    [HttpPost]
    public async Task<IActionResult> Add(AddToCartInput input)
    {
        var (ok, error) = await cart.AddAsync(input.MenuItemId, input.SizeId, input.AddOnIds, input.Quantity);
        if (!ok)
        {
            TempData["CartError"] = error;
            return RedirectToAction(nameof(Customize), new { id = input.MenuItemId });
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public IActionResult Update(string key, int quantity)
    {
        cart.SetQuantity(key, quantity);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public IActionResult Remove(string key)
    {
        cart.Remove(key);
        return RedirectToAction(nameof(Index));
    }
}