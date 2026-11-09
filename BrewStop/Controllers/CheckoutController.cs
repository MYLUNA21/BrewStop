using System.Globalization;
using System.Security.Claims;
using BrewStop.Data;
using BrewStop.Models;
using BrewStop.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace BrewStop.Controllers;

[Authorize]
public class CheckoutController(
    ICartService cart,
    IPickupSlotService slots,
    ICaptchaService captcha,
    IInventoryService inventory,
    IAuditService audit,
    ApplicationDbContext db,
    ILogger<CheckoutController> log) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var c = await cart.GetAsync();
        if (c.Lines.Count == 0) return RedirectToAction("Index", "Cart");

        if (c.HasStockProblem)
        {
            TempData["CartError"] = "Some items in your cart are no longer available in the quantity you chose.";
            return RedirectToAction("Index", "Cart");
        }

        return View(Build(c, new CheckoutInput()));
    }

    [HttpPost]
    public async Task<IActionResult> Place([Bind(Prefix = "Input")] CheckoutInput input)
    {
        var ct = HttpContext.RequestAborted;

        var c = await cart.GetAsync();
        if (c.Lines.Count == 0) return RedirectToAction("Index", "Cart");

        // SRS-FR-07: verified before anything is written.
        if (!await captcha.VerifyAsync(Request, "checkout", ct))
        {
            ModelState.AddModelError(string.Empty, "Captcha verification failed. Please try again.");
            return View("Index", Build(c, input));
        }

        DateTime pickupUtc = default;
        if (input.PickupSlot is null || !TryParseSlot(input.PickupSlot, out pickupUtc) || !slots.IsValid(pickupUtc))
            ModelState.AddModelError("Input.PickupSlot", "Please choose an available pickup time.");

        if (!ModelState.IsValid)
            return View("Index", Build(c, input));

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

        var order = new Order
        {
            UserId = userId,
            PickupAtUtc = pickupUtc,
            Note = SanitizeNote(input.Note),
            Status = OrderStatus.Placed,
            CreatedAtUtc = DateTime.UtcNow
        };

        foreach (var l in c.Lines)
        {
            order.Items.Add(new OrderItem
            {
                MenuItemId = l.MenuItemId,
                ItemName = l.Name,
                SizeName = l.SizeName,
                UnitPrice = l.UnitPrice,
                Quantity = l.Quantity,
                AddOns = l.AddOns.Select(a => new OrderItemAddOn
                {
                    AddOnId = a.Id,
                    Name = a.Name,
                    Price = a.Price
                }).ToList()
            });
        }
        order.Total = order.Items.Sum(i => i.UnitPrice * i.Quantity);

        // SRS-NFR-09 / FR-15: stock deduction and order creation commit together or not at all.
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var wanted = c.Lines.GroupBy(l => l.MenuItemId)
                            .ToDictionary(g => g.Key, g => g.Sum(l => l.Quantity));

        var result = await inventory.DeductAsync(wanted, ct);
        if (!result.Ok)
        {
            await tx.RollbackAsync(ct);

            var names = c.Lines.Where(l => result.ShortItemIds.Contains(l.MenuItemId))
                               .Select(l => l.Name).Distinct().ToList();
            log.LogWarning("Checkout rejected for user {UserId}: insufficient stock for {Items}",
                userId, string.Join(", ", names));

            ModelState.AddModelError(string.Empty,
                $"Sorry, not enough stock for: {string.Join(", ", names)}. Please update your cart.");
            return View("Index", Build(await cart.GetAsync(), input));
        }

        db.Orders.Add(order);
        await db.SaveChangesAsync(ct);   // order.Id is available after this

        // SRS-FR-20: audit entries are saved inside the same transaction.
        audit.Add(userId, "OrderPlaced", $"OrderId={order.Id}; Status=Placed; Total={order.Total:0.00}");
        foreach (var (itemId, qty) in wanted)
            audit.Add(userId, "InventoryDeducted", $"MenuItemId={itemId}; Qty={qty}; OrderId={order.Id}");
        foreach (var itemId in result.SoldOutItemIds)
            audit.Add(null, "ItemSoldOut", $"MenuItemId={itemId}; OrderId={order.Id}");
        await db.SaveChangesAsync(ct);

        await tx.CommitAsync(ct);
        cart.Clear();

        log.LogInformation("Order {OrderId} placed by {UserId}, total {Total}", order.Id, userId, order.Total);
        return RedirectToAction("Confirmation", "Order", new { id = order.Id });
    }

    private CheckoutViewModel Build(CartViewModel c, CheckoutInput input) => new()
    {
        Cart = c,
        Slots = slots.GetAvailableSlots(),
        Input = input
    };

    private static bool TryParseSlot(string value, out DateTime utc) =>
        DateTime.TryParseExact(value, "yyyy-MM-dd'T'HH:mm'Z'", CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out utc);

    private static string? SanitizeNote(string? note)
    {
        if (string.IsNullOrWhiteSpace(note)) return null;

        var cleaned = new string(note.Where(ch => !char.IsControl(ch) || ch is '\n' or '\r').ToArray())
            .Replace("<", "").Replace(">", "")
            .Trim();

        return cleaned.Length == 0 ? null : cleaned;
    }
}