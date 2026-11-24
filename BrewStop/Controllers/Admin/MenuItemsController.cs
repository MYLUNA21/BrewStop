using BrewStop.Data;
using BrewStop.Models;
using BrewStop.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace BrewStop.Controllers.Admin;

[Authorize(Roles = AppRoles.Admin)]
[Route("Admin/MenuItems")]
public class MenuItemsController(ApplicationDbContext db, IWebHostEnvironment env, IMemoryCache cache) : Controller
{
    private const string FormView = "~/Views/Admin/MenuItems/Form.cshtml";
    private const string UploadUrl = "/uploads/menu/";
    private const long MaxBytes = 2 * 1024 * 1024;
    private static readonly string[] AllowedExt = [".jpg", ".jpeg", ".png", ".webp"];

    private void InvalidateMenuCache()
    {
        cache.Remove(CacheKeys.MenuCatalog);
        cache.Remove(CacheKeys.ItemSizes);
        cache.Remove(CacheKeys.AddOns);
    }

    private async Task LoadCategoriesAsync(int? selected = null) =>
        ViewBag.Categories = new SelectList(await db.Categories.AsNoTracking().OrderBy(c => c.Name).ToListAsync(), "Id", "Name", selected);

    private static string? ValidateImage(IFormFile? file)
    {
        if (file is null || file.Length == 0) return null;
        if (file.Length > MaxBytes) return "Image must be 2 MB or smaller.";
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedExt.Contains(ext) || !file.ContentType.StartsWith("image/"))
            return "Only JPG, PNG or WEBP images are allowed.";
        return null;
    }

    private async Task<string> SaveImageAsync(IFormFile file)
    {
        var dir = Path.Combine(env.WebRootPath, "uploads", "menu");
        Directory.CreateDirectory(dir);
        var name = $"{Guid.NewGuid():N}{Path.GetExtension(file.FileName).ToLowerInvariant()}";
        await using var fs = System.IO.File.Create(Path.Combine(dir, name));
        await file.CopyToAsync(fs);
        return UploadUrl + name;
    }

    private void DeleteImage(string? url)
    {
        if (string.IsNullOrEmpty(url) || !url.StartsWith(UploadUrl)) return;
        var path = Path.Combine(env.WebRootPath, "uploads", "menu", Path.GetFileName(url));
        if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
    }

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        ViewBag.Stock = await db.InventoryItems.AsNoTracking().ToDictionaryAsync(i => i.MenuItemId, i => i.Quantity);
        return View("~/Views/Admin/MenuItems/Index.cshtml",
            await db.MenuItems.AsNoTracking().Include(m => m.Category)
                .OrderBy(m => m.Category.Name).ThenBy(m => m.Name).ToListAsync());
    }

    [HttpGet("Create")]
    public async Task<IActionResult> Create()
    {
        await LoadCategoriesAsync();
        return View(FormView, new MenuItem());
    }

    [HttpPost("Create"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("Name,Description,Price,BadgeText,CategoryId")] MenuItem m, IFormFile? image)
    {
        ModelState.Remove(nameof(MenuItem.Category));
        ModelState.Remove(nameof(MenuItem.ImageUrl));
        if (ValidateImage(image) is { } err) ModelState.AddModelError(string.Empty, err);

        if (!ModelState.IsValid)
        {
            await LoadCategoriesAsync(m.CategoryId);
            return View(FormView, m);
        }

        if (image is { Length: > 0 }) m.ImageUrl = await SaveImageAsync(image);

        db.MenuItems.Add(m);
        db.InventoryItems.Add(new InventoryItem { MenuItem = m, Quantity = 0 });
        await db.SaveChangesAsync();
        InvalidateMenuCache();

        TempData["Success"] = $"{m.Name} created.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet("Edit/{id:int}")]
    public async Task<IActionResult> Edit(int id)
    {
        var m = await db.MenuItems.FindAsync(id);
        if (m is null) return NotFound();
        await LoadCategoriesAsync(m.CategoryId);
        return View(FormView, m);
    }

    [HttpPost("Edit/{id:int}"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, [Bind("Id,Name,Description,Price,BadgeText,CategoryId")] MenuItem input,
        IFormFile? image, bool removeImage = false)
    {
        if (id != input.Id) return BadRequest();
        ModelState.Remove(nameof(MenuItem.Category));
        ModelState.Remove(nameof(MenuItem.ImageUrl));

        var m = await db.MenuItems.FindAsync(id);
        if (m is null) return NotFound();

        if (ValidateImage(image) is { } err) ModelState.AddModelError(string.Empty, err);

        if (!ModelState.IsValid)
        {
            input.ImageUrl = m.ImageUrl;
            await LoadCategoriesAsync(input.CategoryId);
            return View(FormView, input);
        }

        m.Name = input.Name;
        m.Description = input.Description;
        m.Price = input.Price;
        m.BadgeText = input.BadgeText;
        m.CategoryId = input.CategoryId;

        if (image is { Length: > 0 })
        {
            DeleteImage(m.ImageUrl);
            m.ImageUrl = await SaveImageAsync(image);
        }
        else if (removeImage)
        {
            DeleteImage(m.ImageUrl);
            m.ImageUrl = string.Empty;
        }

        await db.SaveChangesAsync();
        InvalidateMenuCache();

        TempData["Success"] = $"{m.Name} updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("Delete/{id:int}"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var m = await db.MenuItems.FindAsync(id);
        if (m is null) return NotFound();

        if (await db.OrderItems.AnyAsync(i => i.MenuItemId == id))
        {
            TempData["Error"] = $"{m.Name} has existing orders and cannot be deleted.";
            return RedirectToAction(nameof(Index));
        }

        var imageUrl = m.ImageUrl;
        db.MenuItems.Remove(m);
        await db.SaveChangesAsync();
        InvalidateMenuCache();
        DeleteImage(imageUrl);

        TempData["Success"] = $"{m.Name} deleted.";
        return RedirectToAction(nameof(Index));
    }
}