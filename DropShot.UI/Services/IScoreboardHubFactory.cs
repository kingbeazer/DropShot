using Microsoft.AspNetCore.SignalR.Client;

namespace DropShot.UI.Services;

/// <summary>
/// Builds the SignalR <see cref="HubConnection"/> for the scoreboard hub.
/// Web hosts hand the connection a relative URL via NavigationManager; MAUI
/// uses an absolute URL (App:BaseUrl) and attaches the JWT bearer token via
/// AccessTokenProvider so the hub auth pipeline picks up the active session.
/// </summary>
public interface IScoreboardHubFactory
{
    HubConnection Create();
}
