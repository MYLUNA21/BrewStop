using System.Security.Claims;
using BrewStop.Data;
using BrewStop.Models;
using BrewStop.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace BrewStop.Models
{
    public record CartRecommendationDto(
        int Id,
        string Name,
        string Description,
        decimal Price,
        string ImageUrl,
        string CategoryName,
        bool IsDrink,
        int? DefaultSizeId);

    public class CartPageViewModel : CartViewModel
    {
        public Dictionary<int, string> Descriptions { get; set; } = new();
        public HashSet<int> FavoriteIds { get; set; } = new();
        public bool IsAuthenticated { get; set; }
        public bool CanFavorite { get; set; }
        public List<CartRecommendationDto> Recommendations { get; set; } = new();

        public string? PromoCode { get; set; }
        public string? PromoTitle { get; set; }
        public decimal Discount { get; set; }
        public string? PromoError { get; set; }

        public bool HasPromo => !string.IsNullOrEmpty(PromoCode);
        public decimal EstimatedTotal => Math.Max(0m, Total - Discount);
        public bool HasSoldOut => Lines.Any(l => l.StockLeft <= 0);
    }
}

namespace BrewStop.Controllers
{
    public class CartController(
        ICartService cart,
        IInventoryService inventory,
        IImageResolver images,
        ApplicationDbContext db,
        IMemoryCache cache) : Controller
    {
        private const string PromoKey = "brewstop.cart.promo";

        private sealed record PromoPreview(bool Ok, string? Error, string? Code, string? Title, decimal Discount);

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var c = await cart.GetAsync();
            var vm = new CartPageViewModel { Lines = c.Lines };

            vm.IsAuthenticated = User.Identity?.IsAuthenticated == true;
            vm.CanFavorite = vm.IsAuthenticated
                && (User.IsInRole(AppRoles.Student) || User.IsInRole(AppRoles.Faculty));
            vm.PromoError = TempData["PromoError"] as string;

            if (vm.Lines.Count == 0)
            {
                HttpContext.Session.Remove(PromoKey);
                return View(vm);
            }

            var ids = vm.Lines.Select(l => l.MenuItemId).Distinct().ToList();

            var meta = await db.MenuItems.AsNoTracking()
                .Where(m => ids.Contains(m.Id))
                .Select(m => new { m.Id, m.Description, Group = m.Category.Group })
                .ToListAsync();
            vm.Descriptions = meta.ToDictionary(m => m.Id, m => m.Description);

            if (vm.CanFavorite)
            {
                var uid = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
                var favs = await db.FavoriteItems.AsNoTracking()
                    .Where(f => f.UserId == uid && ids.Contains(f.MenuItemId))
                    .Select(f => f.MenuItemId)
                    .ToListAsync();
                vm.FavoriteIds = favs.ToHashSet();
            }

            var savedCode = HttpContext.Session.GetString(PromoKey);
            if (!string.IsNullOrEmpty(savedCode))
            {
                var preview = await PreviewPromoAsync(savedCode, vm.Total);
                if (preview.Ok)
                {
                    vm.PromoCode = preview.Code;
                    vm.PromoTitle = preview.Title;
                    vm.Discount = preview.Discount;
                }
                else
                {
                    HttpContext.Session.Remove(PromoKey);
                    vm.PromoError ??= preview.Error;
                }
            }

            vm.Recommendations = await BuildRecommendationsAsync(
                ids, meta.Select(m => m.Group).ToHashSet());

            return View(vm);
        }

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

        // Read-only preview: never increments Promotion.TimesUsed (that only happens when the order is placed).
        [HttpPost]
        public async Task<IActionResult> ApplyPromo(string? promoCode)
        {
            var code = promoCode?.Trim().ToUpperInvariant();

            if (string.IsNullOrEmpty(code))
            {
                TempData["PromoError"] = "Enter a promo code first.";
                return RedirectToAction(nameof(Index));
            }

            if (code.Length > 30)
            {
                TempData["PromoError"] = "Invalid promo code.";
                return RedirectToAction(nameof(Index));
            }

            var c = await cart.GetAsync();
            if (c.Lines.Count == 0) return RedirectToAction(nameof(Index));

            var preview = await PreviewPromoAsync(code, c.Total);
            if (preview.Ok)
            {
                HttpContext.Session.SetString(PromoKey, preview.Code!);
            }
            else
            {
                HttpContext.Session.Remove(PromoKey);
                TempData["PromoError"] = preview.Error;
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public IActionResult RemovePromo()
        {
            HttpContext.Session.Remove(PromoKey);
            return RedirectToAction(nameof(Index));
        }

        private async Task<PromoPreview> PreviewPromoAsync(string code, decimal subtotal)
        {
            static PromoPreview Fail(string msg) => new(false, msg, null, null, 0m);

            var normalized = code.Trim().ToUpperInvariant();
            var now = DateTime.UtcNow;

            var promo = await cache.FindActivePromoAsync(db, normalized);
            if (promo is null || !promo.IsActive) return Fail("Invalid promo code.");
            if (promo.ExpiryDate < now) return Fail("This promo code has expired.");
            if (promo.MaxUses is not null && promo.TimesUsed >= promo.MaxUses)
                return Fail("This promo code has reached its usage limit.");

            var discount = promo.DiscountType == DiscountType.Percentage
                ? Math.Round(subtotal * promo.DiscountValue / 100m, 2, MidpointRounding.AwayFromZero)
                : promo.DiscountValue;
            discount = Math.Min(discount, subtotal);

            return new PromoPreview(true, null, promo.Code, promo.Title, discount);
        }

        private async Task<List<CartRecommendationDto>> BuildRecommendationsAsync(
            List<int> cartItemIds, HashSet<string> cartGroups)
        {
            var candidates = await db.MenuItems.AsNoTracking()
                .Where(m => !cartItemIds.Contains(m.Id)
                            && db.InventoryItems.Any(i => i.MenuItemId == m.Id && i.Quantity > 0))
                .Select(m => new
                {
                    m.Id,
                    m.Name,
                    m.Description,
                    m.Price,
                    m.ImageUrl,
                    CategoryName = m.Category.Name,
                    Group = m.Category.Group,
                    Sold = db.OrderItems.Where(o => o.MenuItemId == m.Id).Sum(o => (int?)o.Quantity) ?? 0
                })
                .ToListAsync();

            if (candidates.Count == 0) return new();

            var defaultSize = await db.ItemSizes.AsNoTracking()
                .Where(s => s.IsActive)
                .OrderBy(s => s.SortOrder)
                .FirstOrDefaultAsync();

            // Drinks need a size to be added in one click; skip them if no active size exists.
            var ranked = candidates
                .Where(x => x.Group != "Drinks" || defaultSize is not null)
                .OrderByDescending(x => x.Sold)
                .ThenBy(x => x.Name)
                .ToList();

            // Favor the category group the cart is missing, then fill with the rest.
            var complement = ranked.Where(x => !cartGroups.Contains(x.Group)).ToList();
            var same = ranked.Where(x => cartGroups.Contains(x.Group)).ToList();

            return complement.Take(2)
                .Concat(same.Take(2))
                .Concat(complement.Skip(2))
                .Concat(same.Skip(2))
                .Take(4)
                .Select(x =>
                {
                    var isDrink = x.Group == "Drinks";
                    return new CartRecommendationDto(
                        x.Id,
                        x.Name,
                        x.Description,
                        x.Price + (isDrink ? defaultSize!.PriceDelta : 0m),
                        images.Resolve(x.ImageUrl, x.Name),
                        x.CategoryName,
                        isDrink,
                        isDrink ? defaultSize!.Id : null);
                })
                .ToList();
        }
    }
}