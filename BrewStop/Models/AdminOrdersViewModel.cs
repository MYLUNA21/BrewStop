namespace BrewStop.Models;

public class AdminOrdersViewModel
{
    public List<Order> Orders { get; set; } = new();
    public string Filter { get; set; } = "All";
    public int PendingCount { get; set; }
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