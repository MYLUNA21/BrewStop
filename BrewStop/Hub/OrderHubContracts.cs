using BrewStop.Models;

namespace BrewStop.Hubs;

public record OrderLineSummary(
    string Name,
    int Quantity,
    IReadOnlyList<string> Customizations,
    string? SpecialInstructions);

/// <summary>Pushed to the barista dashboard when a customer places an order.</summary>
public record NewOrderNotification(
    int OrderId,
    string OrderNumber,
    string CustomerName,
    decimal TotalAmount,
    PaymentMethod PaymentMethod,
    DateTime PlacedAtUtc,
    DateTime? EstimatedReadyAtUtc,
    IReadOnlyList<OrderLineSummary> Items);

/// <summary>Pushed to the customer tracking the order and to all barista dashboards.</summary>
public record OrderStatusUpdate(
    int OrderId,
    string OrderNumber,
    OrderStatus Status,
    DateTime UpdatedAtUtc,
    DateTime? EstimatedReadyAtUtc,
    string Message);

/// <summary>Live queue counters for dashboard header tiles.</summary>
public record QueueSnapshot(int Pending, int Preparing, int Ready);

/// <summary>Methods the server can invoke on connected JavaScript clients.</summary>
public interface IOrderClient
{
    Task ReceiveNewOrder(NewOrderNotification order);
    Task OrderStatusChanged(OrderStatusUpdate update);
    Task QueueUpdated(QueueSnapshot snapshot);
}
