using System.ComponentModel.DataAnnotations;
namespace BrewStop.Models;

public class OrderQrToken
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public Order Order { get; set; } = null!;

    [Required, MaxLength(32)] public string Nonce { get; set; } = string.Empty;
    [Required, MaxLength(64)] public string Signature { get; set; } = string.Empty;
    public DateTime ExpiresAtUtc { get; set; }
    public bool IsUsed { get; set; }
    public bool IsRevoked { get; set; }
    public DateTime? ClaimedAtUtc { get; set; }
    [MaxLength(64)] public string? ClaimedBy { get; set; }
}