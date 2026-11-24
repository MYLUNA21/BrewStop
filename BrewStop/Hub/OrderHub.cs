using BrewStop.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace BrewStop.Hubs;

[Authorize]
public class OrderHub(ICustomerNotifier notifier) : Hub
{
    public override async Task OnConnectedAsync()
    {
        var isStaff = Context.User?.IsInRole("Barista") == true || Context.User?.IsInRole("Admin") == true;

        if (Context.UserIdentifier is { } uid)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"user:{uid}");

            if (!isStaff)
                await Clients.Caller.SendAsync("UnreadCountChanged",
                    new { unreadCount = await notifier.UnreadCountAsync(uid) });
        }

        if (isStaff)
            await Groups.AddToGroupAsync(Context.ConnectionId, "staff");

        await base.OnConnectedAsync();
    }
}