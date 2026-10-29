using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;

namespace BrewStop.Models;

[Table("Orders")]
[Index(nameof(OrderNumber), IsUnique = true)]
[Index(nameof(Status), nameof(CreatedAt))]
public class Order : IAuditable
{
    // Unambiguous alphabet (no I, O, 0, 1) so codes are easy to read aloud at the counter.
    private const string CodeAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    private static readonly Regex OrderNumberRegex =
        new(@"^BS-\d{6}-[A-HJ-NP-Z2-9]{4}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    [Key]
    public int Id { get; set; }

    /// <summary>Public, human-friendly reference, e.g. <c>BS-261020-K7M2</c>.</summary>
    [Required, StringLength(20)]
    public string OrderNumber { get; set; } = string.Empty;

    [Required, StringLength(100, MinimumLength = 2)]
    [Display(Name = "Name")]
    public string CustomerName { get; set; } = string.Empty;

    /// <summary>Student or employee ID (optional).</summary>
    [StringLength(20)]
    [Display(Name = "ID number")]
    public string? IdNumber { get; set; }

    [Phone, StringLength(20)]
    public string? ContactNumber { get; set; }

    [Required]
    public OrderStatus Status { get; set; } = OrderStatus.Pending;

    [Required]
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.CashOnPickup;

    public bool IsPaid { get; set; }

    [Range(typeof(decimal), "0", "99999.99")]
    [Column(TypeName = "decimal(10,2)")]
    [DataType(DataType.Currency)]
    public decimal TotalAmount { get; set; }

    [StringLength(300)]
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? EstimatedReadyAt { get; set; }
    public DateTime? ReadyAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    // Navigation
    public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();

    [NotMapped]
    public int ItemCount => Items.Sum(i => i.Quantity);

    /// <summary>Generates <c>BS-yyMMdd-XXXX</c>. The unique index is the final collision guard; retry on conflict.</summary>
    public static string GenerateOrderNumber(DateTime? utcNow = null)
    {
        var date = (utcNow ?? DateTime.UtcNow).ToString("yyMMdd");
        Span<char> code = stackalloc char[4];
        for (var i = 0; i < code.Length; i++)
            code[i] = CodeAlphabet[RandomNumberGenerator.GetInt32(CodeAlphabet.Length)];
        return $"BS-{date}-{new string(code)}";
    }

    public static bool IsValidOrderNumber(string? value) =>
        !string.IsNullOrWhiteSpace(value) && OrderNumberRegex.IsMatch(value.Trim().ToUpperInvariant());
}
