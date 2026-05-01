using DropShot.UI.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;

namespace DropShot.Services;

/// <summary>
/// Web implementation of <see cref="IScoreboardHubFactory"/>. Builds the
/// connection against the in-process <c>/chathub</c> route resolved through
/// <see cref="NavigationManager"/>; cookie auth flows automatically.
/// </summary>
public sealed class WebScoreboardHubFactory(NavigationManager nav) : IScoreboardHubFactory
{
    public HubConnection Create() =>
        new HubConnectionBuilder()
            .WithUrl(nav.ToAbsoluteUri("/chathub"))
            .WithAutomaticReconnect()
            .Build();
}
