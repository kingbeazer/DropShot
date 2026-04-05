using Microsoft.AspNetCore.SignalR;

namespace DropShot.Hubs;

public class QrAuthHub : Hub
{
    public async Task JoinSession(string token)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"qr-{token}");
    }

    public async Task LeaveSession(string token)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"qr-{token}");
    }
}
