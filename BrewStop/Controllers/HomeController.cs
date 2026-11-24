using System.Diagnostics;
using System.Security.Claims;
using BrewStop.Data;
using BrewStop.Models;
using BrewStop.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BrewStop.Models
{
    public record DashboardItemDto(int Id, string Name, string Description, decimal Price, string ImageUrl, string CategoryName);

    public class DashboardViewModel
    {
        public string FirstName { get; set; } = string.Empty;
        public string Greeting { get; set; } = "Hello";

        public int TotalOrders { get; set; }
        public int Points { get; set; }
        public int FavoriteCount { get; set; }
        public DateTime? LastOrderLocal { get; set; }

        public List<Order> ActiveOrders { get; set; } = new();
        public int ActiveCount { get; set; }

        public List<DashboardItemDto> Popular { get; set; } = new();
        public List<DashboardItemDto> Recommended { get; set; } = new();
        public bool HasHistory { get; set; }

        public string HeroImageUrl { get; set; } = string.Empty;

        public decimal PesosPerPoint { get; set; }
        public RewardTier? NextReward { get; set; }
        public int ProgressPercent { get; set; }
    }
}

namespace BrewStop.Controllers
{
    public class HomeController(
        IAuditService audit,
        ApplicationDbContext db,
        IImageResolver images,
        IPickupSlotService slots) : Controller
    {
        public async Task<IActionResult> Index()
        {
            if (User.IsInRole(AppRoles.Admin))
                return RedirectToAction("Dashboard", "Admin");

            var isCustomer = User.Identity?.IsAuthenticated == true
                && (User.IsInRole(AppRoles.Student) || User.IsInRole(AppRoles.Faculty));

            // Guests, baristas and everyone else keep the existing public homepage.
            if (!isCustomer) return View();

            return View("Dashboard", await BuildDashboardAsync(HttpContext.RequestAborted));
        }

        private async Task<DashboardViewModel> BuildDashboardAsync(CancellationToken ct)
        {
            var uid = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

            var me = await db.Users.AsNoTracking()
                .Where(u => u.Id == uid)
                .Select(u => new { u.FullName, u.LoyaltyPoints })
                .FirstOrDefaultAsync(ct);

            var first = (me?.FullName ?? string.Empty)
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .FirstOrDefault() ?? "there";

            var hour = slots.ToLocal(DateTime.UtcNow).Hour;
            var greeting = hour < 12 ? "Good morning" : hour < 18 ? "Good afternoon" : "Good evening";

            var points = me?.LoyaltyPoints ?? 0;

            var totalOrders = await db.Orders.AsNoTracking().CountAsync(o => o.UserId == uid, ct);
            var lastOrderUtc = await db.Orders.AsNoTracking()
                .Where(o => o.UserId == uid)
                .OrderByDescending(o => o.CreatedAtUtc)
                .Select(o => (DateTime?)o.CreatedAtUtc)
                .FirstOrDefaultAsync(ct);
            var favCount = await db.FavoriteItems.AsNoTracking().CountAsync(f => f.UserId == uid, ct);

            var activeQuery = db.Orders.AsNoTracking()
                .Where(o => o.UserId == uid
                            && (o.Status == OrderStatus.Placed
                                || o.Status == OrderStatus.Preparing
                                || o.Status == OrderStatus.Ready));
            var activeCount = await activeQuery.CountAsync(ct);
            var active = await activeQuery
                .Include(o => o.Items).ThenInclude(i => i.AddOns)
                .Include(o => o.Items).ThenInclude(i => i.MenuItem)
                .OrderByDescending(o => o.CreatedAtUtc)
                .Take(3)
                .ToListAsync(ct);

            // Available items (stock > 0) with all-time popularity.
            var candidates = await db.MenuItems.AsNoTracking()
                .Where(m => db.InventoryItems.Any(i => i.MenuItemId == m.Id && i.Quantity > 0))
                .Select(m => new
                {
                    m.Id,
                    m.Name,
                    m.Description,
                    m.Price,
                    m.ImageUrl,
                    m.CategoryId,
                    CategoryName = m.Category.Name,
                    Sold = db.OrderItems
                        .Where(o => o.MenuItemId == m.Id && o.Order.Status != OrderStatus.Cancelled)
                        .Sum(o => (int?)o.Quantity) ?? 0
                })
                .ToListAsync(ct);

            var ranked = candidates.OrderByDescending(c => c.Sold).ThenBy(c => c.Name).ToList();
            var popular = ranked.Take(3).ToList();
            var popularIds = popular.Select(p => p.Id).ToHashSet();
            var rest = ranked.Where(c => !popularIds.Contains(c.Id)).ToList();

            // Past orders drive the recommendations: untried items from the categories ordered most.
            var mine = await db.OrderItems.AsNoTracking()
                .Where(o => o.Order.UserId == uid && o.Order.Status != OrderStatus.Cancelled)
                .Select(o => new { o.MenuItemId, o.MenuItem.CategoryId, o.Quantity })
                .ToListAsync(ct);
            var hasHistory = mine.Count > 0;

            var recommended = rest;
            if (hasHistory)
            {
                var orderedIds = mine.Select(x => x.MenuItemId).ToHashSet();
                var catRank = mine.GroupBy(x => x.CategoryId)
                    .OrderByDescending(g => g.Sum(x => x.Quantity))
                    .Select((g, i) => new { g.Key, Rank = i })
                    .ToDictionary(x => x.Key, x => x.Rank);

                recommended = rest
                    .OrderBy(c => orderedIds.Contains(c.Id))
                    .ThenBy(c => catRank.TryGetValue(c.CategoryId, out var r) ? r : int.MaxValue)
                    .ThenByDescending(c => c.Sold)
                    .ToList();
            }

            DashboardItemDto Map(string name, int id, string desc, decimal price, string img, string cat) =>
                new(id, name, desc, price, images.Resolve(img, name), cat);

            // Next reward tier is real: LoyaltyTiers + ApplicationUser.LoyaltyPoints.
            var next = LoyaltyTiers.All.OrderBy(t => t.Cost).FirstOrDefault(t => t.Cost > points);
            var progress = next is null ? 100 : Math.Clamp((int)Math.Floor(points * 100m / next.Cost), 0, 100);

            return new DashboardViewModel
            {
                FirstName = first,
                Greeting = greeting,
                TotalOrders = totalOrders,
                Points = points,
                FavoriteCount = favCount,
                LastOrderLocal = lastOrderUtc is DateTime d ? slots.ToLocal(d) : null,
                ActiveOrders = active,
                ActiveCount = activeCount,
                Popular = popular.Select(c => Map(c.Name, c.Id, c.Description, c.Price, c.ImageUrl, c.CategoryName)).ToList(),
                Recommended = recommended.Take(4).Select(c => Map(c.Name, c.Id, c.Description, c.Price, c.ImageUrl, c.CategoryName)).ToList(),
                HasHistory = hasHistory,
                HeroImageUrl = images.Resolve(null, "Caramel Latte"),
                PesosPerPoint = LoyaltyTiers.PesosPerPoint,
                NextReward = next,
                ProgressPercent = progress
            };
        }

        public IActionResult Privacy() => View();

        [HttpGet, AllowAnonymous]
        public IActionResult Support() => View(new SupportViewModel());

        [HttpPost, AllowAnonymous, ValidateAntiForgeryToken]
        public async Task<IActionResult> Support(SupportViewModel m)
        {
            if (!ModelState.IsValid) return View(m);

            // Stored in AuditLog (no new table/migration). Swap for IEmailService to notify the shop by email.
            await audit.LogAsync(User.FindFirstValue(ClaimTypes.NameIdentifier), "SupportMessage",
                $"Topic={m.Topic}; From={m.Name} <{m.Email}>; Msg={m.Message.Replace('\r', ' ').Replace('\n', ' ')}",
                HttpContext.RequestAborted);

            TempData["Success"] = "Thanks! Your message was sent. We'll reply by email soon.";
            return RedirectToAction(nameof(Support), null, "contact");
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
            => View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}