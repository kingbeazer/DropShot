using DropShot.Hubs;
using Microsoft.AspNetCore.SignalR;
using NSubstitute;

namespace DropShot.Tests.Helpers;

/// <summary>
/// A no-op IHubContext&lt;MessagingHub&gt; for service-level tests — no real
/// hub runs, so Clients.User/.Group are stubbed to a proxy whose SendAsync
/// no-ops (NSubstitute auto-completes Task-typed calls).
/// </summary>
public static class FakeHubContext
{
    public static IHubContext<MessagingHub> Create()
    {
        var proxy = Substitute.For<IClientProxy>();
        var clients = Substitute.For<IHubClients>();
        clients.User(Arg.Any<string>()).Returns(proxy);
        clients.Group(Arg.Any<string>()).Returns(proxy);

        var hubContext = Substitute.For<IHubContext<MessagingHub>>();
        hubContext.Clients.Returns(clients);
        return hubContext;
    }
}
