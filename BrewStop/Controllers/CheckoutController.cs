using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Security.Claims;
using System.Text.RegularExpressions;
using BrewStop.Data;
using BrewStop.Models;
using BrewStop.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace BrewStop.Controllers;

public class CheckoutContact
{
    public string? FullName { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
}

[Authorize(Policy = "CustomerOnly")]
public class CheckoutController(
    ICartService cart,
    IPickupSlotService slots,
    ICaptchaService captcha,
    IInventoryService inventory,
    IAuditService audit,
    IQrTokenService qr,
    IPromotionService promotions,
    UserManager<ApplicationUser> users,
    ApplicationDbContext db,
    IMemoryCache cache,
    ILogger<CheckoutController> log) : Controller
{
    // Methods the checkout may offer. Card methods (Visa/Mastercard) are intentionally absent:
    // there is no payment-gateway integration in this codebase, and raw card data must never be handled here.
    // A method is offered only when it is ALSO defined in the PaymentMethod enum (backend-supported).
    private static readonly string[] Offered = ["cash", "gcash", "maya", "qrph"];

    private static readonly Regex PhoneRx = new(@"^\+?[0-9 \-]{7,20}$", RegexOptions.Compiled);

    private static string Norm(string s) =>
        new string(s.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();

    private static IReadOnlyList<PaymentMethod> SupportedMethods() =>
        Enum.GetValues<PaymentMethod>().Where(m => Offered.Contains(Norm(m.ToString()))).ToList();

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

        var user = await users.GetUserAsync(User);
        return Render(c, new CheckoutInput(), ContactFrom(user), terms: false, methodChosen: false);
    }

    [HttpPost("/Order/Checkout")]
    public async Task<IActionResult> Place(
        [Bind(Prefix = "Input")] CheckoutInput input,
        [Bind(Prefix = "Contact")] CheckoutContact? contact,
        [FromForm(Name = "AcceptTerms")] bool acceptTerms)
    {
        var ct = HttpContext.RequestAborted;
        input ??= new CheckoutInput();

        var c = await cart.GetAsync();
        if (c.Lines.Count == 0) return RedirectToAction("Index", "Cart");

        var user = await users.GetUserAsync(User);

        if (contact is null || !Request.Form.Keys.Any(k => k.StartsWith("Contact.", StringComparison.Ordinal)))
            contact = ContactFrom(user);
        contact.FullName = (contact.FullName ?? "").Trim();
        contact.Email = (contact.Email ?? "").Trim();
        contact.Phone = (contact.Phone ?? "").Trim();
        ValidateContact(contact);

        DateTime pickupUtc = default;
        if (input.PickupSlot is null || !TryParseSlot(input.PickupSlot, out pickupUtc) || !slots.IsValid(pickupUtc))
            ModelState.AddModelError("Input.PickupSlot", "Please choose an available pickup time.");

        var methodChosen = Request.Form.ContainsKey("Input.PaymentMethod");
        if (!methodChosen || !Enum.IsDefined(input.PaymentMethod) || !SupportedMethods().Contains(input.PaymentMethod))
            ModelState.AddModelError("Input.PaymentMethod", "Please choose a supported payment method.");

        if (!acceptTerms)
            ModelState.AddModelError("AcceptTerms", "Please accept the ordering terms to continue.");

        var contactChanged = user is not null && (
            !Same(contact.FullName, user.FullName) ||
            !Same(contact.Email, user.Email) ||
            !Same(contact.Phone, user.PhoneNumber));

        var contactLine = contactChanged
            ? SanitizeNote($"Pickup contact: {contact.FullName}"
                           + (contact.Phone.Length > 0 ? $" | {contact.Phone}" : "")
                           + $" | {contact.Email}")
            : null;
        var combinedNote = string.Join("\n", new[] { contactLine, SanitizeNote(input.Note) }.Where(s => s is not null));
        if (combinedNote.Length > 200)
            ModelState.AddModelError("Input.Note", "Your note plus edited contact details exceed 200 characters. Please shorten your note.");

        if (!ModelState.IsValid)
            return Render(c, input, contact, acceptTerms, methodChosen);

        if (!await captcha.VerifyAsync(Request, "checkout", ct))
        {
            ModelState.AddModelError(string.Empty, "Captcha verification failed. Please try again.");
            return Render(c, input, contact, acceptTerms, methodChosen);
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

        var reference = input.PaymentMethod == PaymentMethod.GCash
            ? input.PaymentReference?.Trim().ToUpperInvariant()
            : null;
        if (string.IsNullOrWhiteSpace(reference)) reference = null;

        var order = new Order
        {
            UserId = userId,
            PickupAtUtc = pickupUtc,
            Note = combinedNote.Length == 0 ? null : combinedNote,
            Status = OrderStatus.Placed,
            PaymentMethod = input.PaymentMethod,
            PaymentStatus = PaymentStatus.Unpaid,
            PaymentReference = reference,
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
        var subtotal = order.Items.Sum(i => i.UnitPrice * i.Quantity);
        order.Total = subtotal;

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
            return Render(await cart.GetAsync(), input, contact, acceptTerms, true);
        }

        var promo = await promotions.ApplyPromotionAsync(input.PromoCode, subtotal, ct);
        if (!promo.Ok)
        {
            await tx.RollbackAsync(ct);
            ModelState.AddModelError("Input.PromoCode", promo.Error!);
            return Render(await cart.GetAsync(), input, contact, acceptTerms, true);
        }
        order.PromoCode = promo.Code;
        order.DiscountAmount = promo.Discount;
        order.Total = subtotal - promo.Discount;

        db.Orders.Add(order);
        await db.SaveChangesAsync(ct);

        audit.Add(userId, "OrderPlaced",
            $"OrderId={order.Id}; Status=Placed; Total={order.Total:0.00}; Payment={order.PaymentMethod}");
        if (promo.PromotionId is not null)
            audit.Add(userId, "PromoApplied", $"OrderId={order.Id}; Code={promo.Code}; Discount={promo.Discount:0.00}");
        foreach (var (itemId, qty) in wanted)
            audit.Add(userId, "InventoryDeducted", $"MenuItemId={itemId}; Qty={qty}; OrderId={order.Id}");
        foreach (var itemId in result.SoldOutItemIds)
            audit.Add(null, "ItemSoldOut", $"MenuItemId={itemId}; OrderId={order.Id}");
        await db.SaveChangesAsync(ct);

        await qr.IssueAsync(order, ct);

        await tx.CommitAsync(ct);
        cart.Clear();

        log.LogInformation("Order {OrderId} placed by {UserId}, total {Total}", order.Id, userId, order.Total);
        return RedirectToAction("Confirmation", "Order", new { id = order.Id });
    }

    /// <summary>Read-only promo preview.</summary>
    [HttpPost]
    public async Task<IActionResult> PreviewPromo([FromForm] string? code, CancellationToken ct)
    {
        var c = await cart.GetAsync();
        var subtotal = c.Total;

        if (string.IsNullOrWhiteSpace(code))
            return Json(new { ok = false, message = "Enter a promo code.", code = (string?)null, discount = 0m, total = subtotal });

        var normalized = code.Trim().ToUpperInvariant();
        if (normalized.Length > 30)
            return Json(new { ok = false, message = "Invalid promo code.", code = (string?)null, discount = 0m, total = subtotal });

        var now = DateTime.UtcNow;
        var promo = await cache.FindActivePromoAsync(db, normalized, ct);

        string? error =
            promo is null || !promo.IsActive ? "Invalid promo code."
            : promo.ExpiryDate < now ? "This promo code has expired."
            : promo.MaxUses is not null && promo.TimesUsed >= promo.MaxUses ? "This promo code has reached its usage limit."
            : null;

        if (error is not null)
            return Json(new { ok = false, message = error, code = (string?)null, discount = 0m, total = subtotal });

        var discount = promo!.DiscountType == DiscountType.Percentage
            ? Math.Round(subtotal * promo.DiscountValue / 100m, 2, MidpointRounding.AwayFromZero)
            : promo.DiscountValue;
        discount = Math.Min(discount, subtotal);

        return Json(new
        {
            ok = true,
            message = $"{promo.Title} applied.",
            code = promo.Code,
            discount,
            total = subtotal - discount
        });
    }

    private IActionResult Render(CartViewModel c, CheckoutInput input, CheckoutContact contact, bool terms, bool methodChosen)
    {
        ViewData["Methods"] = SupportedMethods().Select(m => m.ToString()).ToList();
        ViewData["MethodChosen"] = methodChosen;
        ViewData["AcceptTerms"] = terms;
        ViewData["Contact"] = contact;
        return View("Index", new CheckoutViewModel
        {
            Cart = c,
            Slots = slots.GetAvailableSlots(),
            Input = input
        });
    }

    private static CheckoutContact ContactFrom(ApplicationUser? u) => new()
    {
        FullName = u?.FullName ?? "",
        Email = u?.Email ?? "",
        Phone = u?.PhoneNumber ?? ""
    };

    private void ValidateContact(CheckoutContact c)
    {
        if (c.FullName!.Length == 0)
            ModelState.AddModelError("Contact.FullName", "Please enter your full name.");
        else if (c.FullName.Length > 100)
            ModelState.AddModelError("Contact.FullName", "Name must be 100 characters or fewer.");

        if (c.Email!.Length == 0 || c.Email.Length > 100 || !new EmailAddressAttribute().IsValid(c.Email))
            ModelState.AddModelError("Contact.Email", "Please enter a valid email address.");

        if (c.Phone!.Length > 0 && !PhoneRx.IsMatch(c.Phone))
            ModelState.AddModelError("Contact.Phone", "Please enter a valid phone number.");
    }

    private static bool Same(string? a, string? b) =>
        string.Equals((a ?? "").Trim(), (b ?? "").Trim(), StringComparison.OrdinalIgnoreCase);

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