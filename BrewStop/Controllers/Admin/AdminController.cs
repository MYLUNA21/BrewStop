using System.Globalization;
using System.Security.Claims;
using System.Text;
using BrewStop.Data;
using BrewStop.Models;
using BrewStop.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BrewStop.Controllers.Admin;

[Authorize(Roles = AppRoles.Admin)]
[Route("Admin")]
public class AdminController(
    ApplicationDbContext db,
    IAuditService audit,
    IQrTokenService qr,
    UserManager<ApplicationUser> users) : Controller
{
    private string? ActorId => User.FindFirstValue(ClaimTypes.NameIdentifier);

    [HttpGet(""), HttpGet("Index")]
    public IActionResult Index() => View("~/Views/Admin/Index.cshtml");

    [HttpGet("Dashboard")]
    public async Task<IActionResult> Dashboard(CancellationToken ct)
    {
        var today = DateTime.UtcNow.Date;
        var weekStart = today.AddDays(-6);
        var monthStart = new DateTime(today.Year, today.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var since = weekStart < monthStart ? weekStart : monthStart;

        var revenueOrders = db.Orders.AsNoTracking().Where(o => o.Status == OrderStatus.Claimed);

        var recent = await revenueOrders.Where(o => o.CreatedAtUtc >= since)
            .Select(o => new { o.CreatedAtUtc, o.Total }).ToListAsync(ct);

        var best = await db.OrderItems.AsNoTracking()
            .Where(i => i.Order.Status != OrderStatus.Cancelled)
            .GroupBy(i => new { i.MenuItemId, i.MenuItem.Name })
            .Select(g => new { g.Key.Name, Units = g.Sum(i => i.Quantity) })
            .OrderByDescending(x => x.Units)
            .FirstOrDefaultAsync(ct);

        var customers = await (from ur in db.UserRoles
                               join r in db.Roles on ur.RoleId equals r.Id
                               where r.Name == AppRoles.Student || r.Name == AppRoles.Faculty
                               select ur.UserId).Distinct().CountAsync(ct);

        var days = Enumerable.Range(0, 7).Select(i => weekStart.AddDays(i)).ToList();

        var vm = new AdminDashboardViewModel
        {
            TotalRevenue = await revenueOrders.SumAsync(o => (decimal?)o.Total, ct) ?? 0,
            TotalCustomers = customers,
            TotalOrders = await db.Orders.CountAsync(o => o.Status != OrderStatus.Cancelled, ct),
            PendingOrders = await db.Orders.CountAsync(o => o.Status == OrderStatus.Placed || o.Status == OrderStatus.Preparing, ct),
            BestSellerName = best?.Name,
            BestSellerUnits = best?.Units ?? 0,
            TodayRevenue = recent.Where(o => o.CreatedAtUtc >= today).Sum(o => o.Total),
            WeekRevenue = recent.Where(o => o.CreatedAtUtc >= weekStart).Sum(o => o.Total),
            MonthRevenue = recent.Where(o => o.CreatedAtUtc >= monthStart).Sum(o => o.Total),
            ChartLabels = days.Select(d => d.ToString("MMM d")).ToList(),
            ChartValues = days.Select(d => recent.Where(o => o.CreatedAtUtc.Date == d).Sum(o => o.Total)).ToList()
        };
        return View(vm);
    }

    [HttpGet("Orders")]
    public async Task<IActionResult> Orders(string? status, CancellationToken ct)
    {
        var q = db.Orders.AsNoTracking().Include(o => o.User).AsQueryable();
        var filter = "All";
        if (Enum.TryParse<OrderStatus>(status, true, out var s) && Enum.IsDefined(s))
        {
            q = q.Where(o => o.Status == s);
            filter = s.ToString();
        }

        return View(new AdminOrdersViewModel
        {
            Filter = filter,
            Orders = await q.OrderByDescending(o => o.CreatedAtUtc).ToListAsync(ct),
            PendingCount = await db.Orders.CountAsync(o => o.Status == OrderStatus.Placed || o.Status == OrderStatus.Preparing, ct)
        });
    }

    [HttpGet("OrderDetails/{id:int}")]
    public async Task<IActionResult> OrderDetails(int id, CancellationToken ct)
    {
        var order = await db.Orders.AsNoTracking()
            .Include(o => o.User)
            .Include(o => o.Items).ThenInclude(i => i.AddOns)
            .FirstOrDefaultAsync(o => o.Id == id, ct);
        return order is null ? NotFound() : View(order);
    }

    [HttpPost("UpdateOrderStatus"), ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateOrderStatus(int id, OrderStatus status, CancellationToken ct)
    {
        var order = await db.Orders.AsNoTracking().Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == id, ct);
        if (order is null) return NotFound();

        var from = order.Status;
        if (!AdminOrderRules.NextOptions(from).Contains(status))
        {
            TempData["Error"] = $"Order #{id} cannot move from {from} to {status}.";
            return RedirectToAction(nameof(OrderDetails), new { id });
        }

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var rows = await db.Orders.Where(o => o.Id == id && o.Status == from)
            .ExecuteUpdateAsync(s => s.SetProperty(o => o.Status, status), ct);
        if (rows == 0)
        {
            await tx.RollbackAsync(ct);
            TempData["Error"] = $"Order #{id} changed in the meantime. Please review it again.";
            return RedirectToAction(nameof(OrderDetails), new { id });
        }

        var cancelled = status == OrderStatus.Cancelled && from != OrderStatus.Cancelled;
        if (cancelled)
        {
            foreach (var g in order.Items.GroupBy(i => i.MenuItemId).OrderBy(g => g.Key))
            {
                var itemId = g.Key;
                var qty = g.Sum(i => i.Quantity);
                await db.InventoryItems.Where(i => i.MenuItemId == itemId)
                    .ExecuteUpdateAsync(s => s.SetProperty(i => i.Quantity, i => i.Quantity + qty), ct);
            }
            await qr.RevokeAsync(id, ct);
        }

        audit.Add(ActorId,
            cancelled ? "OrderCancelled" : "OrderStatusChanged",
            $"OrderId={id}; {from}->{status}");
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        TempData["Success"] = $"Order #{id} is now {status}.";
        return RedirectToAction(nameof(OrderDetails), new { id });
    }

    [HttpGet("Menu")]
    public IActionResult Menu() => RedirectToAction("Index", "MenuItems");

    // ---------------- Customer Management ----------------

    [HttpGet("Customers")]
    public async Task<IActionResult> Customers(string? q, CancellationToken ct)
    {
        var roleMap = (await (from ur in db.UserRoles
                              join r in db.Roles on ur.RoleId equals r.Id
                              select new { ur.UserId, r.Name }).ToListAsync(ct))
            .GroupBy(x => x.UserId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Name!).ToList());

        var stats = (await db.Orders.AsNoTracking()
                .Where(o => o.Status != OrderStatus.Cancelled)
                .GroupBy(o => o.UserId)
                .Select(g => new
                {
                    UserId = g.Key,
                    Count = g.Count(),
                    Spent = g.Sum(o => o.Total),
                    Completed = g.Count(o => o.Status == OrderStatus.Claimed)
                }).ToListAsync(ct))
            .ToDictionary(x => x.UserId);

        var now = DateTimeOffset.UtcNow;
        var rows = (await db.Users.AsNoTracking().OrderBy(u => u.FullName).ToListAsync(ct))
            .Select(u =>
            {
                stats.TryGetValue(u.Id, out var s);
                return new AdminCustomerRow
                {
                    Id = u.Id,
                    FullName = u.FullName,
                    CampusId = u.CampusId,
                    Email = u.Email,
                    Roles = roleMap.GetValueOrDefault(u.Id) ?? new List<string>(),
                    EmailConfirmed = u.EmailConfirmed,
                    IsLocked = u.LockoutEnd.HasValue && u.LockoutEnd > now,
                    TotalOrders = s?.Count ?? 0,
                    TotalSpent = s?.Spent ?? 0,
                    IsFrequent = (s?.Completed ?? 0) >= 5
                };
            });

        if (!string.IsNullOrWhiteSpace(q))
        {
            var t = q.Trim();
            bool Has(string? v) => v?.Contains(t, StringComparison.OrdinalIgnoreCase) == true;
            rows = rows.Where(r => Has(r.FullName) || Has(r.Email) || Has(r.CampusId) || r.Roles.Any(Has));
        }

        return View(new AdminCustomersViewModel { Rows = rows.ToList(), Query = q });
    }

    [HttpGet("CustomerDetails/{id}")]
    public async Task<IActionResult> CustomerDetails(string id, CancellationToken ct)
    {
        var user = await users.FindByIdAsync(id);
        if (user is null) return NotFound();

        return View(new AdminCustomerDetailsViewModel
        {
            User = user,
            Roles = (await users.GetRolesAsync(user)).ToList(),
            IsLocked = user.LockoutEnd.HasValue && user.LockoutEnd > DateTimeOffset.UtcNow,
            Orders = await db.Orders.AsNoTracking()
                .Where(o => o.UserId == id)
                .OrderByDescending(o => o.CreatedAtUtc)
                .ToListAsync(ct)
        });
    }

    [HttpPost("UpdateUserRole"), ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateUserRole(string id, string role, CancellationToken ct)
    {
        var user = await users.FindByIdAsync(id);
        if (user is null) return NotFound();

        if (!AppRoles.All.Contains(role))
        {
            TempData["Error"] = "Invalid role.";
            return RedirectToAction(nameof(CustomerDetails), new { id });
        }

        var current = (await users.GetRolesAsync(user)).ToList();
        if (current.Count == 1 && current[0] == role)
        {
            TempData["Error"] = $"User is already {role}.";
            return RedirectToAction(nameof(CustomerDetails), new { id });
        }

        if (current.Contains(AppRoles.Admin) && role != AppRoles.Admin
            && (await users.GetUsersInRoleAsync(AppRoles.Admin)).Count <= 1)
        {
            TempData["Error"] = "Cannot remove the role of the last Admin.";
            return RedirectToAction(nameof(CustomerDetails), new { id });
        }

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var ok = true;
        if (current.Count > 0) ok = (await users.RemoveFromRolesAsync(user, current)).Succeeded;
        ok = ok && (await users.AddToRoleAsync(user, role)).Succeeded;
        if (!ok)
        {
            await tx.RollbackAsync(ct);
            TempData["Error"] = "Could not update the role.";
            return RedirectToAction(nameof(CustomerDetails), new { id });
        }

        await users.UpdateSecurityStampAsync(user);
        audit.Add(ActorId, "UserRoleChanged", $"UserId={id}; {string.Join(",", current)}->{role}");
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        TempData["Success"] = $"{user.FullName} is now {role}.";
        return RedirectToAction(nameof(CustomerDetails), new { id });
    }

    [HttpPost("ToggleUserLock"), ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleUserLock(string id, CancellationToken ct)
    {
        var user = await users.FindByIdAsync(id);
        if (user is null) return NotFound();

        var locked = user.LockoutEnd.HasValue && user.LockoutEnd > DateTimeOffset.UtcNow;

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        IdentityResult result;
        if (locked)
        {
            result = await users.SetLockoutEndDateAsync(user, null);
            if (result.Succeeded) await users.ResetAccessFailedCountAsync(user);
        }
        else
        {
            await users.SetLockoutEnabledAsync(user, true);
            result = await users.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue);
        }

        if (!result.Succeeded)
        {
            await tx.RollbackAsync(ct);
            TempData["Error"] = "Could not update the account lock.";
            return RedirectToAction(nameof(CustomerDetails), new { id });
        }

        audit.Add(ActorId, locked ? "UserUnlocked" : "UserLocked", $"UserId={id}; CampusId={user.CampusId}");
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        TempData["Success"] = $"{user.FullName} has been {(locked ? "unlocked" : "locked")}.";
        return RedirectToAction(nameof(CustomerDetails), new { id });
    }

    // ---------------- Payment Management ----------------

    [HttpGet("Payments")]
    public async Task<IActionResult> Payments(string? paymentStatus, string? method, string? date, CancellationToken ct)
    {
        var today = DateTime.UtcNow.Date;
        var all = db.Orders.AsNoTracking();

        async Task<decimal> Sum(IQueryable<Order> src) =>
            await src.SumAsync(o => (decimal?)o.Total, ct) ?? 0;

        var q = all.Include(o => o.User).AsQueryable();
        var vm = new AdminPaymentsViewModel();

        if (Enum.TryParse<PaymentStatus>(paymentStatus, true, out var ps) && Enum.IsDefined(ps))
        {
            q = q.Where(o => o.PaymentStatus == ps);
            vm.PaymentStatusFilter = ps.ToString();
        }
        if (Enum.TryParse<PaymentMethod>(method, true, out var pm) && Enum.IsDefined(pm))
        {
            q = q.Where(o => o.PaymentMethod == pm);
            vm.MethodFilter = pm.ToString();
        }
        if (DateTime.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var d))
        {
            var from = d.Date;
            var to = from.AddDays(1);
            q = q.Where(o => o.CreatedAtUtc >= from && o.CreatedAtUtc < to);
            vm.Date = from.ToString("yyyy-MM-dd");
        }

        vm.Orders = await q.OrderByDescending(o => o.CreatedAtUtc).ToListAsync(ct);
        vm.TodayCollected = await Sum(all.Where(o => o.PaymentStatus == PaymentStatus.Paid && o.PaidAtUtc >= today));
        vm.UnpaidPending = await Sum(all.Where(o => o.PaymentStatus == PaymentStatus.Unpaid
            && (o.Status == OrderStatus.Placed || o.Status == OrderStatus.Preparing || o.Status == OrderStatus.Ready)));
        vm.CashTotal = await Sum(all.Where(o => o.PaymentStatus == PaymentStatus.Paid && o.PaymentMethod == PaymentMethod.Cash));
        vm.GCashTotal = await Sum(all.Where(o => o.PaymentStatus == PaymentStatus.Paid && o.PaymentMethod == PaymentMethod.GCash));
        vm.RefundedTotal = await Sum(all.Where(o => o.PaymentStatus == PaymentStatus.Refunded));

        return View(vm);
    }

    [HttpPost("UpdatePaymentStatus"), ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdatePaymentStatus(
        int id, PaymentStatus status, string? fp, string? fm, string? fd, CancellationToken ct)
    {
        IActionResult Back() => RedirectToAction(nameof(Payments), new { paymentStatus = fp, method = fm, date = fd });

        var order = await db.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.Id == id, ct);
        if (order is null) return NotFound();

        var from = order.PaymentStatus;
        var allowed = (from, status) is (PaymentStatus.Unpaid, PaymentStatus.Paid) or (PaymentStatus.Paid, PaymentStatus.Refunded);
        if (!allowed || (status == PaymentStatus.Paid && order.Status == OrderStatus.Cancelled))
        {
            TempData["Error"] = $"Order #{id} payment cannot move from {from} to {status}.";
            return Back();
        }

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var now = DateTime.UtcNow;
        var rows = status == PaymentStatus.Paid
            ? await db.Orders.Where(o => o.Id == id && o.PaymentStatus == from)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(o => o.PaymentStatus, status)
                    .SetProperty(o => o.PaidAtUtc, now), ct)
            : await db.Orders.Where(o => o.Id == id && o.PaymentStatus == from)
                .ExecuteUpdateAsync(s => s.SetProperty(o => o.PaymentStatus, status), ct);

        if (rows == 0)
        {
            await tx.RollbackAsync(ct);
            TempData["Error"] = $"Order #{id} changed in the meantime. Please review it again.";
            return Back();
        }

        audit.Add(ActorId, "PaymentStatusChanged",
            $"OrderId={id}; {from}->{status}; Method={order.PaymentMethod}; Amount={order.Total:0.00}");
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        TempData["Success"] = $"Order #{id} marked {status}.";
        return Back();
    }

    // ---------------- Discounts & Promotions ----------------

    private async Task<AdminPromotionsViewModel> BuildPromotionsVm(CreatePromotionInput? input, CancellationToken ct) => new()
    {
        Promotions = await db.Promotions.AsNoTracking().OrderByDescending(p => p.Id).ToListAsync(ct),
        Input = input ?? new CreatePromotionInput()
    };

    [Authorize(Roles = AppRoles.Admin)]
    [HttpGet("Promotions")]
    public async Task<IActionResult> Promotions(CancellationToken ct) =>
        View(await BuildPromotionsVm(null, ct));

    [Authorize(Roles = AppRoles.Admin)]
    [HttpPost("CreatePromotion"), ValidateAntiForgeryToken]
    public async Task<IActionResult> CreatePromotion([Bind(Prefix = "Input")] CreatePromotionInput input, CancellationToken ct)
    {
        var code = input.Code?.Trim().ToUpperInvariant() ?? "";

        if (input.DiscountType == DiscountType.Percentage && input.DiscountValue > 100)
            ModelState.AddModelError("Input.DiscountValue", "Percentage cannot exceed 100.");
        if (input.ExpiryDate is { } exp && exp.Date < DateTime.UtcNow.Date)
            ModelState.AddModelError("Input.ExpiryDate", "Expiry date cannot be in the past.");
        if (code.Length > 0 && await db.Promotions.AnyAsync(p => p.Code == code, ct))
            ModelState.AddModelError("Input.Code", "This code already exists.");

        if (!ModelState.IsValid)
            return View("Promotions", await BuildPromotionsVm(input, ct));

        var promo = new Promotion
        {
            Code = code,
            Title = input.Title.Trim(),
            DiscountType = input.DiscountType,
            DiscountValue = input.DiscountValue,
            ExpiryDate = DateTime.SpecifyKind(input.ExpiryDate!.Value.Date.AddDays(1).AddSeconds(-1), DateTimeKind.Utc),
            MaxUses = input.MaxUses,
            IsActive = true
        };
        db.Promotions.Add(promo);
        await db.SaveChangesAsync(ct);

        await audit.LogAsync(ActorId, "PromotionCreated",
            $"PromotionId={promo.Id}; Code={promo.Code}; {promo.DiscountType}={promo.DiscountValue:0.00}; Expires={promo.ExpiryDate:yyyy-MM-dd}; MaxUses={promo.MaxUses?.ToString() ?? "none"}", ct);

        TempData["Success"] = $"Promotion {promo.Code} created.";
        return RedirectToAction(nameof(Promotions));
    }

    [Authorize(Roles = AppRoles.Admin)]
    [HttpPost("TogglePromotion"), ValidateAntiForgeryToken]
    public async Task<IActionResult> TogglePromotion(int id, CancellationToken ct)
    {
        var promo = await db.Promotions.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (promo is null) return NotFound();

        promo.IsActive = !promo.IsActive;
        audit.Add(ActorId, "PromotionToggled", $"PromotionId={id}; Code={promo.Code}; IsActive={promo.IsActive}");
        await db.SaveChangesAsync(ct);

        TempData["Success"] = $"{promo.Code} is now {(promo.IsActive ? "active" : "inactive")}.";
        return RedirectToAction(nameof(Promotions));
    }

    // ---------------- Reports & Records ----------------

    private static (DateTime Start, DateTime End) ResolveRange(DateTime? startDate, DateTime? endDate)
    {
        var today = DateTime.UtcNow.Date;
        var start = (startDate ?? new DateTime(today.Year, today.Month, 1)).Date;
        var end = (endDate ?? today).Date;
        if (end < start) (start, end) = (end, start);
        return (start, end);
    }

    private async Task<AdminReportsViewModel> BuildReportAsync(DateTime start, DateTime end, CancellationToken ct)
    {
        var to = end.AddDays(1);
        var orders = await db.Orders.AsNoTracking()
            .Include(o => o.User).Include(o => o.Items)
            .Where(o => o.CreatedAtUtc >= start && o.CreatedAtUtc < to)
            .OrderByDescending(o => o.CreatedAtUtc)
            .ToListAsync(ct);

        var sales = orders.Where(o => o.Status != OrderStatus.Cancelled).ToList();

        static AdminSalesRow Row(string label, IEnumerable<Order> g)
        {
            var l = g.ToList();
            return new AdminSalesRow(label, l.Count, l.Sum(o => o.Total + o.DiscountAmount),
                l.Sum(o => o.DiscountAmount), l.Sum(o => o.Total));
        }

        var sold = sales.SelectMany(o => o.Items).GroupBy(i => i.MenuItemId)
            .ToDictionary(g => g.Key, g => (Units: g.Sum(i => i.Quantity), Revenue: g.Sum(i => i.UnitPrice * i.Quantity)));

        var perf = (await db.MenuItems.AsNoTracking().Select(m => new { m.Id, m.Name }).ToListAsync(ct))
            .Select(m => sold.TryGetValue(m.Id, out var s)
                ? new AdminProductRow(m.Name, s.Units, s.Revenue)
                : new AdminProductRow(m.Name, 0, 0m))
            .ToList();

        return new AdminReportsViewModel
        {
            StartDate = start,
            EndDate = end,
            Daily = sales.GroupBy(o => o.CreatedAtUtc.Date).OrderBy(g => g.Key)
                .Select(g => Row(g.Key.ToString("yyyy-MM-dd"), g)).ToList(),
            Monthly = sales.GroupBy(o => new { o.CreatedAtUtc.Year, o.CreatedAtUtc.Month })
                .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month)
                .Select(g => Row($"{g.Key.Year}-{g.Key.Month:00}", g)).ToList(),
            MostPopular = perf.OrderByDescending(p => p.Units).ThenByDescending(p => p.Revenue).ThenBy(p => p.Name).Take(5).ToList(),
            LeastPopular = perf.OrderBy(p => p.Units).ThenBy(p => p.Revenue).ThenBy(p => p.Name).Take(5).ToList(),
            Transactions = orders
        };
    }

    [Authorize(Roles = AppRoles.Admin)]
    [HttpGet("Reports")]
    public async Task<IActionResult> Reports(DateTime? startDate, DateTime? endDate, CancellationToken ct)
    {
        var (start, end) = ResolveRange(startDate, endDate);
        return View(await BuildReportAsync(start, end, ct));
    }

    [Authorize(Roles = AppRoles.Admin)]
    [HttpGet("ExportReportsCsv")]
    public async Task<IActionResult> ExportReportsCsv(DateTime? startDate, DateTime? endDate, CancellationToken ct)
    {
        var (start, end) = ResolveRange(startDate, endDate);
        var r = await BuildReportAsync(start, end, ct);

        static string Q(string? v)
        {
            v ??= "";
            if (v.Length > 0 && "=+-@\t\r".Contains(v[0])) v = "'" + v; // CSV formula-injection guard
            return "\"" + v.Replace("\"", "\"\"") + "\"";
        }
        static string N(decimal d) => d.ToString("0.00", CultureInfo.InvariantCulture);

        var sb = new StringBuilder();
        sb.AppendLine($"BrewStop Report,{start:yyyy-MM-dd} to {end:yyyy-MM-dd}");
        sb.AppendLine();

        void Sales(string title, string col, List<AdminSalesRow> rows)
        {
            sb.AppendLine(title);
            sb.AppendLine($"{col},Order Count,Gross Sales,Discounts,Net Revenue");
            foreach (var x in rows) sb.AppendLine($"{Q(x.Label)},{x.Orders},{N(x.Gross)},{N(x.Discounts)},{N(x.Net)}");
            sb.AppendLine();
        }
        Sales("Daily Sales Summary", "Date", r.Daily);
        Sales("Monthly Revenue Summary", "Month", r.Monthly);

        void Products(string title, List<AdminProductRow> rows)
        {
            sb.AppendLine(title);
            sb.AppendLine("Product,Units Sold,Revenue");
            foreach (var x in rows) sb.AppendLine($"{Q(x.Name)},{x.Units},{N(x.Revenue)}");
            sb.AppendLine();
        }
        Products("Most Popular Products", r.MostPopular);
        Products("Least Popular Products", r.LeastPopular);

        sb.AppendLine("Transaction Records");
        sb.AppendLine("Order ID,Date/Time (UTC),Customer,Items,Payment Method,Status,Total");
        foreach (var o in r.Transactions)
            sb.AppendLine($"{o.Id},{o.CreatedAtUtc:yyyy-MM-dd HH:mm},{Q(o.User.FullName)},{Q(AdminReportsViewModel.ItemsSummary(o))},{o.PaymentMethod},{o.Status},{N(o.Total)}");

        return File(Encoding.UTF8.GetBytes("\uFEFF" + sb),
            "text/csv", $"BrewStop_Report_{start:yyyyMMdd}_{end:yyyyMMdd}.csv");
    }
}