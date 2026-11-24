namespace BrewStop.Models;

public class AdminOrdersViewModel
{
    public List<AdminOrderBoardRow> Orders { get; set; } = new();
    public string Filter { get; set; } = "All";
    public int PendingCount { get; set; }
    public int HistoryLimit { get; set; } = 60;
}

public class AdminOrderBoardRow
{
    public int Id { get; set; }
    public OrderStatus Status { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string CampusId { get; set; } = string.Empty;
    public PaymentMethod PaymentMethod { get; set; }
    public PaymentStatus PaymentStatus { get; set; }
    public string? PaymentReference { get; set; }
    public string? Note { get; set; }
    public string? PromoCode { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal Total { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime PickupAtUtc { get; set; }
    public List<AdminOrderBoardItem> Items { get; set; } = new();
}

public class AdminOrderBoardItem
{
    public string ItemName { get; set; } = string.Empty;
    public string? SizeName { get; set; }
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
    public List<AdminOrderBoardAddOn> AddOns { get; set; } = new();
}

public class AdminOrderBoardAddOn
{
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
}

public static class AdminOrderRules
{
    public static OrderStatus[] NextOptions(OrderStatus s) => s switch
    {
        OrderStatus.Placed => [OrderStatus.Preparing, OrderStatus.Cancelled],
        OrderStatus.Preparing => [OrderStatus.Ready, OrderStatus.Cancelled],
        OrderStatus.Ready => [OrderStatus.Claimed, OrderStatus.Cancelled],
        _ => []
    };
}