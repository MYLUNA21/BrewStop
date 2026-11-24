using BrewStop.Data;
using BrewStop.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BrewStop.Controllers.Admin;

[Authorize(Roles = "Admin")]
[Route("Admin/AuditLogs")]
public class AuditLogsController : Controller
{
    private readonly ApplicationDbContext _db;

    public AuditLogsController(ApplicationDbContext db)
    {
        _db = db;
    }

    public record AuditLogRow(
        long Id,
        DateTime CreatedAtUtc,
        string? UserId,
        string ActorDisplay,
        string Action,
        string? Details);

    public class AuditLogsViewModel
    {
        public string? Search { get; set; }
        public string? ActionFilter { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public int Page { get; set; } = 1;
        public int TotalPages { get; set; } = 1;
        public int TotalCount { get; set; }
        public List<string> DistinctActions { get; set; } = new();
        public List<AuditLogRow> Logs { get; set; } = new();
    }

    [HttpGet("")]
    [HttpGet("Index")]
    public async Task<IActionResult> Index(
        string? search,
        string? actionFilter,
        DateTime? startDate,
        DateTime? endDate,
        int page = 1)
    {
        const int pageSize = 25;
        page = Math.Max(1, page);

        var query = _db.AuditLogs.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(actionFilter))
        {
            query = query.Where(a => a.Action == actionFilter);
        }

        if (startDate.HasValue)
        {
            var startUtc = DateTime.SpecifyKind(startDate.Value.Date, DateTimeKind.Utc);
            query = query.Where(a => a.CreatedAtUtc >= startUtc);
        }

        if (endDate.HasValue)
        {
            var endExclusiveUtc = DateTime.SpecifyKind(endDate.Value.Date.AddDays(1), DateTimeKind.Utc);
            query = query.Where(a => a.CreatedAtUtc < endExclusiveUtc);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            var matchingUserIds = await _db.Users
                .AsNoTracking()
                .Where(u => (u.FullName != null && u.FullName.Contains(term)) ||
                            (u.Email != null && u.Email.Contains(term)))
                .Select(u => u.Id)
                .ToListAsync();

            query = query.Where(a =>
                a.Action.Contains(term) ||
                (a.Details != null && a.Details.Contains(term)) ||
                (a.UserId != null && (a.UserId.Contains(term) || matchingUserIds.Contains(a.UserId))));
        }

        var totalCount = await query.CountAsync();
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
        if (page > totalPages) page = totalPages;

        var rawLogs = await query
            .OrderByDescending(a => a.CreatedAtUtc)
            .ThenByDescending(a => a.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var userIds = rawLogs
            .Where(l => !string.IsNullOrEmpty(l.UserId))
            .Select(l => l.UserId!)
            .Distinct()
            .ToList();

        var usersById = await _db.Users
            .AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(
                u => u.Id,
                u => string.IsNullOrWhiteSpace(u.FullName) ? (u.Email ?? u.Id) : $"{u.FullName} ({u.Email})");

        var rows = rawLogs.Select(l =>
        {
            string actor;
            if (string.IsNullOrWhiteSpace(l.UserId))
                actor = "System";
            else if (usersById.TryGetValue(l.UserId, out var label))
                actor = label;
            else
                actor = $"Deleted User ({l.UserId[..Math.Min(8, l.UserId.Length)]}…)";

            return new AuditLogRow(l.Id, l.CreatedAtUtc, l.UserId, actor, l.Action, l.Details);
        }).ToList();

        var distinctActions = await _db.AuditLogs
            .AsNoTracking()
            .Select(a => a.Action)
            .Distinct()
            .OrderBy(a => a)
            .ToListAsync();

        var vm = new AuditLogsViewModel
        {
            Search = search,
            ActionFilter = actionFilter,
            StartDate = startDate,
            EndDate = endDate,
            Page = page,
            TotalPages = totalPages,
            TotalCount = totalCount,
            DistinctActions = distinctActions,
            Logs = rows
        };

        return View("~/Views/Admin/AuditLogs/Index.cshtml", vm);
    }
}