using BrewStop.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace BrewStop.Hubs;

/// <summary>
/// Real-time channel for BrewStop.
///  • Customers call <see cref="TrackOrder"/> to follow a single order.
///  • Baristas (Admin role) call <see cref="JoinBaristaDashboard"/> to receive every order and status change.
/// Server-to-client pushes are sent through <c>IOrderNotifier</c>, not by clients invoking the hub.
/// </summary>
public class OrderHub : Hub<IOrderClient>
{
    public const string BaristaGroup = "baristas";
    public const string AdminRole = "Admin";

    private readonly ILogger<OrderHub> _logger;

    public OrderHub(ILogger<OrderHub> logger) => _logger = logger;

    public static string OrderGroup(string orderNumber) => $"order:{orderNumber.Trim().ToUpperInvariant()}";

    // ---------- Customer ----------

    public async Task TrackOrder(string orderNumber)
    {
        if (!Order.IsValidOrderNumber(orderNumber))
            throw new HubException("Invalid order number.");

        await Groups.AddToGroupAsync(Context.ConnectionId, OrderGroup(orderNumber));
        _logger.LogDebug("Connection {ConnectionId} tracking {OrderNumber}", Context.ConnectionId, orderNumber);
    }

    public async Task StopTrackingOrder(string orderNumber)
    {
        if (!Order.IsValidOrderNumber(orderNumber))
            throw new HubException("Invalid order number.");

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, OrderGroup(orderNumber));
    }

    // ---------- Barista / Admin ----------
    // NOTE: requires authentication + an "Admin" role to be configured (Identity/cookie auth).
    // Until auth is wired up, comment out the [Authorize] attribute for local development only.

    [Authorize(Roles = AdminRole)]
    public Task JoinBaristaDashboard() =>
        Groups.AddToGroupAsync(Context.ConnectionId, BaristaGroup);

    [Authorize(Roles = AdminRole)]
    public Task LeaveBaristaDashboard() =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, BaristaGroup);

    // ---------- Lifecycle ----------

    public override Task OnConnectedAsync()
    {
        _logger.LogDebug("SignalR connected: {ConnectionId}", Context.ConnectionId);
        return base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        if (exception is not null)
            _logger.LogWarning(exception, "SignalR disconnected with error: {ConnectionId}", Context.ConnectionId);

        // SignalR removes the connection from all groups automatically.
        return base.OnDisconnectedAsync(exception);
    }
}
