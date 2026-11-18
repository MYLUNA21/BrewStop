using System.Security.Claims;
using BrewStop.Data;
using BrewStop.Models;
using BrewStop.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BrewStop.Controllers;

[Authorize(Policy = "CustomerOnly")]
public class ReviewsController(ApplicationDbContext db, IAuditService audit, ILogger<ReviewsController> log) : Controller
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ReviewInput input)
    {
        var ct = HttpContext.RequestAborted;

        IActionResult Back(string key, string message)
        {
            TempData[key] = message;
            return RedirectToAction("Details", "Order", new { id = input.OrderId });
        }

        if (!ModelState.IsValid)
        {
            var first = ModelState.Values.SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage).FirstOrDefault(m => !string.IsNullOrEmpty(m));
            return Back("ReviewError", first ?? "Please choose a rating from 1 to 5.");
        }

        // Ownership (SRS-FR-18): another user's order is indistinguishable from a missing one.
        var order = await db.Orders.AsNoTracking()
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == input.OrderId && o.UserId == UserId, ct);
        if (order is null) return NotFound();

        if (order.Status != OrderStatus.Claimed)
            return Back("ReviewError", "You can review items once your order has been picked up.");

        if (!order.Items.Any(i => i.MenuItemId == input.MenuItemId))
            return Back("ReviewError", "That item is not part of this order.");

        if (await db.ProductReviews.AnyAsync(r =>
                r.UserId == UserId && r.OrderId == order.Id && r.MenuItemId == input.MenuItemId, ct))
            return Back("ReviewError", "You already reviewed this item for this order.");

        db.ProductReviews.Add(new ProductReview
        {
            UserId = UserId,
            OrderId = order.Id,
            MenuItemId = input.MenuItemId,
            Rating = input.Rating,
            Comment = Sanitize(input.Comment),
            CreatedAtUtc = DateTime.UtcNow
        });
        audit.Add(UserId, "ReviewCreated",
            $"OrderId={order.Id}; MenuItemId={input.MenuItemId}; Rating={input.Rating}");

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex)  
        {
            log.LogWarning(ex, "Duplicate review rejected for order {OrderId}", order.Id);
            return Back("ReviewError", "You already reviewed this item for this order.");
        }

        return Back("ReviewSuccess", "Thanks for your review!");
    }

    private static string? Sanitize(string? comment)
    {
        if (string.IsNullOrWhiteSpace(comment)) return null;

        var cleaned = new string(comment.Where(ch => !char.IsControl(ch) || ch is '\n' or '\r').ToArray())
            .Replace("<", "").Replace(">", "")
            .Trim();

        if (cleaned.Length == 0) return null;
        return cleaned.Length > 300 ? cleaned[..300] : cleaned;
    }
}