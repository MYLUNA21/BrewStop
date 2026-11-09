using BrewStop.Data;
using BrewStop.Models;

namespace BrewStop.Services;

public interface IAuditService
{
    /// <summary>Stages an entry; it is saved with the caller's next SaveChanges (so it joins a transaction).</summary>
    void Add(string? userId, string action, string? details = null);

    /// <summary>Stages and saves immediately.</summary>
    Task LogAsync(string? userId, string action, string? details = null, CancellationToken ct = default);
}

public class AuditService(ApplicationDbContext db, ILogger<AuditService> log) : IAuditService
{
    public void Add(string? userId, string action, string? details = null)
    {
        db.AuditLogs.Add(new AuditLog
        {
            UserId = userId,
            Action = action,
            Details = details is { Length: > 500 } ? details[..500] : details,
            CreatedAtUtc = DateTime.UtcNow
        });
        log.LogInformation("Audit {Action} by {UserId}: {Details}", action, userId ?? "system", details);
    }

    public async Task LogAsync(string? userId, string action, string? details = null, CancellationToken ct = default)
    {
        Add(userId, action, details);
        await db.SaveChangesAsync(ct);
    }
}