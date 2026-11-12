using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace BrewStop.Hubs;

[Authorize]
public class OrderHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        if (Context.UserIdentifier is { } uid)
            await Groups.AddToGroupAsync(Context.ConnectionId, $"user:{uid}");

        if (Context.User?.IsInRole("Barista") == true || Context.User?.IsInRole("Admin") == true)
            await Groups.AddToGroupAsync(Context.ConnectionId, "staff");

        await base.OnConnectedAsync();
    }
}