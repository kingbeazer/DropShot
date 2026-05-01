namespace DropShot.UI.Services;

/// <summary>
/// Scoreboard domain abstraction. Marker interface at phase 3 — populated in
/// phase 6 alongside Scoreboard / TeamMatchScoring page moves and SignalR hub
/// wiring. Live updates use <c>HubConnection</c> directly via a DI factory;
/// this interface covers REST snapshot reads.
/// </summary>
public interface IScoreboardService
{
}
