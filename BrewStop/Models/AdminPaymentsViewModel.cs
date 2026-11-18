namespace BrewStop.Models;

public class AdminPaymentsViewModel
{
    public List<Order> Orders { get; set; } = new();
    public string PaymentStatusFilter { get; set; } = "All";
    public string MethodFilter { get; set; } = "All";
    public string? Date { get; set; }

    public decimal TodayCollected { get; set; }
    public decimal UnpaidPending { get; set; }
    public decimal CashTotal { get; set; }
    public decimal GCashTotal { get; set; }
    public decimal RefundedTotal { get; set; }
}