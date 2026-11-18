namespace BrewStop.Models;

public class TopSellingDrinkDto
{
    public string MenuItemName { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public int UnitsSold { get; set; }
    public decimal TotalRevenue { get; set; }
}

public class AttentionAlertDto
{
    /// <summary>OutOfStock | LowStock | RejectedScan</summary>
    public string Kind { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
    public string? ActionUrl { get; set; }
    public string? ActionLabel { get; set; }
    public DateTime? OccurredAt { get; set; }   // shop-local time
}

public class RecentOrderRowDto
{
    public int OrderId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string CampusId { get; set; } = string.Empty;
    public DateTime PickupSlot { get; set; }    // shop-local time
    public decimal TotalAmount { get; set; }
    public PaymentMethod PaymentMethod { get; set; }
    public PaymentStatus PaymentStatus { get; set; }
    public OrderStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }     // shop-local time
}

public class AdminDashboardViewModel
{
    // Filter (shop-local dates; default = last 7 days)
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public DateTime NowLocal { get; set; }

    // KPIs
    public decimal RevenueToday { get; set; }
    public decimal RevenueYesterday { get; set; }
    public decimal? RevenueChangePercent { get; set; }   // null when yesterday had no sales
    public decimal AverageOrderValue { get; set; }       // over the selected range
    public int OrdersToday { get; set; }
    public int PendingPickupCount { get; set; }          // Placed + Preparing + Ready, right now
    public decimal RangeRevenue { get; set; }
    public int RangeOrders { get; set; }

    // Charts
    public string[] DailyLabels { get; set; } = Array.Empty<string>();
    public decimal[] DailyRevenueData { get; set; } = Array.Empty<decimal>();
    public int[] DailyOrderCountData { get; set; } = Array.Empty<int>();
    public string[] PaymentMethodLabels { get; set; } = { "Cash", "GCash" };
    public int[] PaymentMethodCounts { get; set; } = new int[2];
    public string[] OrderStatusLabels { get; set; } = Array.Empty<string>();
    public int[] OrderStatusCounts { get; set; } = Array.Empty<int>();

    public List<TopSellingDrinkDto> TopSellingDrinks { get; set; } = new();

    // Shifts
    public int BaristasOnShiftCount { get; set; }
    public int ScheduledShiftsTodayCount { get; set; }
    public int CompletedShiftsTodayCount { get; set; }

    public List<AttentionAlertDto> AttentionAlerts { get; set; } = new();
    public List<RecentOrderRowDto> RecentOrders { get; set; } = new();
}