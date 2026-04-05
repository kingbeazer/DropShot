namespace DropShot.Models
{
    using Microsoft.AspNetCore.SignalR;
    using System.Threading.Tasks;

    public class ChatHub : Hub
    {
        public async Task SendMessage(string user, string message)
        {
            await Clients.All.SendAsync("ReceiveMessage", user, message);
        }

        public async Task JoinCourtGroup(int courtId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"court-{courtId}");
        }

        public async Task LeaveCourtGroup(int courtId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"court-{courtId}");
        }

        public async Task SendDisplayCommand(int courtId, string command, string value)
        {
            await Clients.OthersInGroup($"court-{courtId}").SendAsync("ReceiveDisplayCommand", command, value);
        }
    }
}
