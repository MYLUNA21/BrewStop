using System.Text.Json;
using BrewStop.Data;
using BrewStop.Models;
using Microsoft.EntityFrameworkCore;

namespace BrewStop.Services;

public record CartEntry(string Key, int MenuItemId, int? SizeId, int[] AddOnIds, int Quantity);

public interface ICartService
{
    int Count { get; }
    Task<(bool Ok, string? Error)> AddAsync(int menuItemId, int? sizeId, IEnumerable<int> addOnIds, int quantity);
    void SetQuantity(string key, int quantity);
    void Remove(string key);
    void Clear();
    Task<CartViewModel> GetAsync();
}

public class CartService(ApplicationDbContext db, IHttpContextAccessor http) : ICartService
{
    public const int MaxQuantity = 10;
    public const int MaxLines = 20;
    private const string SessionKey = "brewstop.cart";

    private ISession Session => http.HttpContext!.Session;

    private List<CartEntry> Load()
    {
        var json = Session.GetString(SessionKey);
        if (string.IsNullOrEmpty(json)) return new();
        try { return JsonSerializer.Deserialize<List<CartEntry>>(json) ?? new(); }
        catch (JsonException) { return new(); }
    }

    private void Save(List<CartEntry> entries) =>
        Session.SetString(SessionKey, JsonSerializer.Serialize(entries));

    public int Count => Load().Sum(e => e.Quantity);

    public async Task<(bool Ok, string? Error)> AddAsync(
        int menuItemId, int? sizeId, IEnumerable<int> addOnIds, int quantity)
    {
        quantity = Math.Clamp(quantity, 1, MaxQuantity);

        var item = await db.MenuItems.AsNoTracking()
            .Include(m => m.Category)
            .FirstOrDefaultAsync(m => m.Id == menuItemId);
        if (item is null) return (false, "That item is no longer on the menu.");

        var isDrink = item.Category.Group == "Drinks";

        int? validSizeId = null;
        if (isDrink)
        {
            if (sizeId is null) return (false, "Please choose a size.");
            if (!await db.ItemSizes.AnyAsync(s => s.Id == sizeId && s.IsActive))
                return (false, "That size is not available.");
            validSizeId = sizeId;
        }

        var ids = addOnIds.Distinct().OrderBy(i => i).ToArray();
        if (ids.Length > 0)
        {
            var group = item.Category.Group;

            var idList = ids.ToList();

            var valid = await db.AddOns.CountAsync(a => idList.Contains(a.Id) && a.IsActive && a.Group == group);

            if (valid != ids.Length) return (false, "One or more add-ons are not available for this item.");
        }

        var key = $"{menuItemId}:{validSizeId?.ToString() ?? "-"}:{string.Join(",", ids)}";
        var entries = Load();
        var index = entries.FindIndex(e => e.Key == key);

        if (index >= 0)
            entries[index] = entries[index] with { Quantity = Math.Min(MaxQuantity, entries[index].Quantity + quantity) };
        else
        {
            if (entries.Count >= MaxLines) return (false, "Your cart is full.");
            entries.Add(new CartEntry(key, menuItemId, validSizeId, ids, quantity));
        }

        Save(entries);
        return (true, null);
    }

    public void SetQuantity(string key, int quantity)
    {
        var entries = Load();
        var index = entries.FindIndex(e => e.Key == key);
        if (index < 0) return;

        if (quantity <= 0) entries.RemoveAt(index);
        else entries[index] = entries[index] with { Quantity = Math.Min(quantity, MaxQuantity) };

        Save(entries);
    }

    public void Remove(string key)
    {
        var entries = Load();
        if (entries.RemoveAll(e => e.Key == key) > 0) Save(entries);
    }

    public void Clear() => Session.Remove(SessionKey);

    public async Task<CartViewModel> GetAsync()
    {
        var vm = new CartViewModel();
        var entries = Load();
        if (entries.Count == 0) return vm;

        var itemIds = entries.Select(e => e.MenuItemId).Distinct().ToList();
        var items = await db.MenuItems.AsNoTracking()
            .Where(m => itemIds.Contains(m.Id))
            .ToDictionaryAsync(m => m.Id);
        var sizes = await db.ItemSizes.AsNoTracking().Where(s => s.IsActive).ToDictionaryAsync(s => s.Id);
        var addOns = await db.AddOns.AsNoTracking().Where(a => a.IsActive).ToDictionaryAsync(a => a.Id);

        var kept = new List<CartEntry>();
        foreach (var e in entries)
        {
            if (!items.TryGetValue(e.MenuItemId, out var item)) continue;

            ItemSize? size = null;
            if (e.SizeId is int sid && !sizes.TryGetValue(sid, out size)) continue;

            var lineAddOns = new List<CartAddOnDto>();
            var valid = true;
            foreach (var aid in e.AddOnIds)
            {
                if (addOns.TryGetValue(aid, out var a)) lineAddOns.Add(new CartAddOnDto(a.Id, a.Name, a.Price));
                else { valid = false; break; }
            }
            if (!valid) continue;

            kept.Add(e);
            vm.Lines.Add(new CartLineViewModel
            {
                Key = e.Key,
                MenuItemId = item.Id,
                Name = item.Name,
                ImageUrl = item.ImageUrl,
                SizeId = size?.Id,
                SizeName = size?.Name,
                AddOns = lineAddOns,
                UnitPrice = item.Price + (size?.PriceDelta ?? 0m) + lineAddOns.Sum(a => a.Price),
                Quantity = e.Quantity
            });
        }

        if (kept.Count != entries.Count) Save(kept);
        return vm;
    }
}