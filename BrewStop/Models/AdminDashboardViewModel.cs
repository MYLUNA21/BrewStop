namespace BrewStop.Models;

public class AdminDashboardViewModel
{
    public decimal TotalRevenue { get; set; }
    public int TotalCustomers { get; set; }
    public int TotalOrders { get; set; }
    public int PendingOrders { get; set; }
    public string? BestSellerName { get; set; }
    public int BestSellerUnits { get; set; }
    public decimal TodayRevenue { get; set; }
    public decimal WeekRevenue { get; set; }
    public decimal MonthRevenue { get; set; }
    public List<string> ChartLabels { get; set; } = new();
    public List<decimal> ChartValues { get; set; } = new();
}