using BrewStop.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace BrewStop.Services;

/// <summary>Server-side entry point for pushing real-time order events from controllers/services.</summary>
public interface IOrderNotifier
{
    Task NotifyNewOrderAsync(NewOrderNotification order, CancellationToken ct = default);
    Task NotifyStatusChangedAsync(OrderStatusUpdate update, CancellationToken ct = default);
    Task PublishQueueAsync(QueueSnapshot snapshot, CancellationToken ct = default);
}

public class OrderNotifier : IOrderNotifier
{
    private readonly IHubContext<OrderHub, IOrderClient> _hub;

    public OrderNotifier(IHubContext<OrderHub, IOrderClient> hub) => _hub = hub;

    public Task NotifyNewOrderAsync(NewOrderNotification order, CancellationToken ct = default) =>
        _hub.Clients.Group(OrderHub.BaristaGroup).ReceiveNewOrder(order);

    public Task NotifyStatusChangedAsync(OrderStatusUpdate update, CancellationToken ct = default) =>
        Task.WhenAll(
            _hub.Clients.Group(OrderHub.OrderGroup(update.OrderNumber)).OrderStatusChanged(update),
            _hub.Clients.Group(OrderHub.BaristaGroup).OrderStatusChanged(update));

    public Task PublishQueueAsync(QueueSnapshot snapshot, CancellationToken ct = default) =>
        _hub.Clients.Group(OrderHub.BaristaGroup).QueueUpdated(snapshot);
}
