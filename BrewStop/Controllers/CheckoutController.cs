using System.Globalization;
using System.Security.Claims;
using BrewStop.Data;
using BrewStop.Models;
using BrewStop.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Linq;

namespace BrewStop.Controllers;

[Authorize]
public class CheckoutController(ICartService cart, IPickupSlotService slots, ApplicationDbContext db) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var c = await cart.GetAsync();
        if (c.Lines.Count == 0) return RedirectToAction("Index", "Cart");
        return View(Build(c, new CheckoutInput()));
    }

    [HttpPost]
    public async Task<IActionResult> Place([Bind(Prefix = "Input")] CheckoutInput input)
    {
        var c = await cart.GetAsync();
        if (c.Lines.Count == 0) return RedirectToAction("Index", "Cart");

        DateTime pickupUtc = default;
        if (input.PickupSlot is null || !TryParseSlot(input.PickupSlot, out pickupUtc) || !slots.IsValid(pickupUtc))
            ModelState.AddModelError("Input.PickupSlot", "Please choose an available pickup time.");

        if (!ModelState.IsValid)
            return View("Index", Build(c, input));

        var order = new Order
        {
            UserId = User.FindFirstValue(ClaimTypes.NameIdentifier)!,
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

        db.Orders.Add(order);
        await db.SaveChangesAsync();   
        cart.Clear();

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