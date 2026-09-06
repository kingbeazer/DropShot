using DropShot.UI.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;

namespace DropShot.Services;

/// <summary>
/// Web implementation of <see cref="IMessagingHubFactory"/>. Builds the
/// connection against the in-process <c>/messaginghub</c> route resolved
/// through <see cref="NavigationManager"/>; cookie auth flows automatically.
/// </summary>
public sealed class WebMessagingHubFactory(NavigationManager nav) : IMessagingHubFactory
{
    public HubConnection Create() =>
        new HubConnectionBuilder()
            .WithUrl(nav.ToAbsoluteUri("/messaginghub"))
            .WithAutomaticReconnect()
            .Build();
}
