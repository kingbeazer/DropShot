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
            await Clients.OthersInGroup($"court-{courtId}").SendAsync("ReceiveDisplayCommand", courtId, command, value);
        }

        // ── Court claim challenge flow ──────────────────────────────────
        // When a new user picks a court that has an active match, they
        // call ChallengeCourt. Every existing scorer in the court group
        // receives CourtChallenge with the challenger's connection id so
        // they can reply directly via RespondToCourtChallenge.

        public async Task ChallengeCourt(int courtId, int savedMatchId)
        {
            await Clients.OthersInGroup($"court-{courtId}")
                .SendAsync("CourtChallenge", courtId, savedMatchId, Context.ConnectionId);
        }

        public async Task RespondToCourtChallenge(string challengerConnectionId, int courtId, int savedMatchId, bool stillPlaying)
        {
            await Clients.Client(challengerConnectionId)
                .SendAsync("CourtChallengeResponse", courtId, savedMatchId, stillPlaying);
        }
    }
}
