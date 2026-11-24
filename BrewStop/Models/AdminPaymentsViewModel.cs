namespace BrewStop.Models;

public class AdminPaymentsViewModel
{
    public List<AdminPaymentRow> Orders { get; set; } = new();
    public string PaymentStatusFilter { get; set; } = "All";
    public string MethodFilter { get; set; } = "All";
    public string? Date { get; set; }

    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 15;
    public int TotalCount { get; set; }
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));

    public decimal TodayCollected { get; set; }
    public decimal UnpaidPending { get; set; }
    public decimal CashTotal { get; set; }
    public decimal GCashTotal { get; set; }
    public decimal RefundedTotal { get; set; }
}

public record AdminPaymentRow(
    int Id,
    string CustomerName,
    string CampusId,
    DateTime CreatedAtUtc,
    OrderStatus Status,
    PaymentMethod PaymentMethod,
    decimal Total,
    PaymentStatus PaymentStatus);