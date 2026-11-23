namespace BrewStop.Models;

public record TopItemDto(string Name, int Quantity);
public record ActivityDto(string Title, string Text, string Icon, DateTime AtUtc);
public record BaristaAlertDto(string Kind, string Title, string Detail, int? OrderId, DateTime? AtUtc, bool Urgent = false);

public class BaristaDashboardViewModel
{
    public DateTime LocalNow { get; set; }
    public int Placed { get; set; }
    public int Preparing { get; set; }
    public int Ready { get; set; }
    public int OrdersToday { get; set; }
    public int ClaimedToday { get; set; }
    public int CancelledToday { get; set; }
    public int LowStockCount { get; set; }
    public List<TopItemDto> TopItems { get; set; } = new();
    public List<ActivityDto> Recent { get; set; } = new();
    public BaristaShift? OpenShift { get; set; }
}

public class RecipeGuideViewModel
{
    public List<Category> Categories { get; set; } = new();
    public List<ItemSize> Sizes { get; set; } = new();
    public List<AddOn> AddOns { get; set; } = new();
}

public class ShiftsViewModel
{
    public BaristaShift? Open { get; set; }
    public List<BaristaShift> Upcoming { get; set; } = new();
    public List<BaristaShift> History { get; set; } = new();
    public decimal HoursToday { get; set; }
    public decimal HoursThisWeek { get; set; }
} 