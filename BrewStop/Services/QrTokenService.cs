using System.Security.Cryptography;
using System.Text;
using BrewStop.Data;
using BrewStop.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BrewStop.Services;

public class QrOptions { public string SigningKey { get; set; } = string.Empty; }

public enum ScanResult { Ok, Invalid, Expired, AlreadyUsed, Revoked, WrongStatus }

public interface IQrTokenService
{
    Task<string> IssueAsync(Order order, CancellationToken ct = default);
    Task<(ScanResult Result, int? OrderId)> ClaimAsync(string token, string baristaId, CancellationToken ct = default);
    Task RevokeAsync(int orderId, CancellationToken ct = default);
}

public class QrTokenService(ApplicationDbContext db, IOptions<QrOptions> opt, IAuditService audit) : IQrTokenService
{
    private byte[] Key => string.IsNullOrWhiteSpace(opt.Value.SigningKey)
        ? throw new InvalidOperationException("Qr:SigningKey is not configured (use user-secrets / env var Qr__SigningKey).")
        : Encoding.UTF8.GetBytes(opt.Value.SigningKey);

    private static string B64(byte[] b) => Convert.ToBase64String(b).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    private byte[] Sign(string payload) => HMACSHA256.HashData(Key, Encoding.UTF8.GetBytes(payload));

    public async Task<string> IssueAsync(Order order, CancellationToken ct = default)
    {
        var nonce = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
        var expiry = order.PickupAtUtc.AddMinutes(30);
        var payload = $"{order.Id}|{nonce}|{new DateTimeOffset(expiry, TimeSpan.Zero).ToUnixTimeSeconds()}";
        var sig = B64(Sign(payload));

        db.OrderQrTokens.Add(new OrderQrToken { OrderId = order.Id, Nonce = nonce, Signature = sig, ExpiresAtUtc = expiry });
        await db.SaveChangesAsync(ct);
        return $"{B64(Encoding.UTF8.GetBytes(payload))}.{sig}";
    }

    public async Task<(ScanResult, int?)> ClaimAsync(string token, string baristaId, CancellationToken ct = default)
    {
        async Task<(ScanResult, int?)> Fail(ScanResult r, int? orderId, string why)
        {
            await audit.LogAsync(baristaId, "QrScanRejected", $"OrderId={orderId}; Reason={why}", ct);
            return (r, orderId);
        }

        var parts = token.Split('.');
        if (parts.Length != 2) return await Fail(ScanResult.Invalid, null, "Format");

        string payload;
        try
        {
            var p = parts[0].Replace('-', '+').Replace('_', '/');
            payload = Encoding.UTF8.GetString(Convert.FromBase64String(p.PadRight(p.Length + (4 - p.Length % 4) % 4, '=')));
        }
        catch { return await Fail(ScanResult.Invalid, null, "Encoding"); }

        var expected = Encoding.UTF8.GetBytes(B64(Sign(payload)));
        if (!CryptographicOperations.FixedTimeEquals(expected, Encoding.UTF8.GetBytes(parts[1])))
            return await Fail(ScanResult.Invalid, null, "Signature");

        var f = payload.Split('|');
        if (f.Length != 3 || !int.TryParse(f[0], out var orderId) || !long.TryParse(f[2], out var exp))
            return await Fail(ScanResult.Invalid, null, "Payload");

        if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() > exp) return await Fail(ScanResult.Expired, orderId, "Expired");

        var t = await db.OrderQrTokens.AsNoTracking().Include(x => x.Order)
            .FirstOrDefaultAsync(x => x.OrderId == orderId && x.Nonce == f[1], ct);
        if (t is null) return await Fail(ScanResult.Invalid, orderId, "Nonce");
        if (t.IsRevoked) return await Fail(ScanResult.Revoked, orderId, "Revoked");
        if (t.IsUsed) return await Fail(ScanResult.AlreadyUsed, orderId, "AlreadyUsed");
        if (t.Order.Status != OrderStatus.Ready) return await Fail(ScanResult.WrongStatus, orderId, "Status=" + t.Order.Status);

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        // Atomic claim: only one concurrent scan can flip IsUsed (SRS-NFR-09).
        var rows = await db.OrderQrTokens
            .Where(x => x.Id == t.Id && !x.IsUsed && !x.IsRevoked)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.IsUsed, true)
                .SetProperty(x => x.ClaimedAtUtc, DateTime.UtcNow)
                .SetProperty(x => x.ClaimedBy, baristaId), ct);

        if (rows == 0) { await tx.RollbackAsync(ct); return await Fail(ScanResult.AlreadyUsed, orderId, "Race"); }

        await db.Orders.Where(o => o.Id == orderId)
            .ExecuteUpdateAsync(s => s.SetProperty(o => o.Status, OrderStatus.Claimed), ct);

        audit.Add(baristaId, "QrScanAccepted", $"OrderId={orderId}");
        audit.Add(baristaId, "OrderStatusChanged", $"OrderId={orderId}; Ready->Claimed");
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return (ScanResult.Ok, orderId);
    }

    public Task RevokeAsync(int orderId, CancellationToken ct = default) =>
        db.OrderQrTokens.Where(x => x.OrderId == orderId)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.IsRevoked, true), ct);
}