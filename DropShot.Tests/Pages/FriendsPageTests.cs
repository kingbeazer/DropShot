using Bunit;
using DropShot.Data;
using DropShot.Models;
using DropShot.Shared;
using DropShot.Tests.Helpers;
using DropShot.UI.Components.Pages;
using Xunit;

namespace DropShot.Tests.Pages;

public class FriendsPageTests
{
    [Fact]
    public async Task FriendsPage_Renders_Empty_State()
    {
        await using var ctx = new DropShotTestContext(authenticated: true, userId: "user-a");

        using (var db = ctx.SeedDatabase())
        {
            db.Players.Add(new Player { PlayerId = 1, DisplayName = "Alice", UserId = "user-a" });
            db.SaveChanges();
        }

        var cut = ctx.Render<FriendsPage>();

        Assert.Contains("No friends yet", cut.Markup);
    }

    [Fact]
    public async Task FriendsPage_Shows_Accepted_Friend()
    {
        await using var ctx = new DropShotTestContext(authenticated: true, userId: "user-a");

        using (var db = ctx.SeedDatabase())
        {
            db.Players.Add(new Player { PlayerId = 1, DisplayName = "Alice", UserId = "user-a" });
            db.Players.Add(new Player { PlayerId = 2, DisplayName = "Bob", UserId = "user-b" });
            db.PlayerFriends.Add(new PlayerFriend { PlayerId = 1, FriendPlayerId = 2, Status = FriendStatus.Accepted });
            db.SaveChanges();
        }

        // Friend rows render MudTooltip (Message/Remove buttons), which needs
        // a live MudPopoverProvider ancestor that the bare shell doesn't supply.
        var markup = ctx.RenderWithPopoverProvider<FriendsPage>();

        Assert.Contains("Bob", markup);
    }

    [Fact]
    public async Task FriendsPage_Shows_Incoming_Request_Count()
    {
        await using var ctx = new DropShotTestContext(authenticated: true, userId: "user-a");

        using (var db = ctx.SeedDatabase())
        {
            db.Players.Add(new Player { PlayerId = 1, DisplayName = "Alice", UserId = "user-a" });
            db.Players.Add(new Player { PlayerId = 2, DisplayName = "Bob", UserId = "user-b" });
            db.PlayerFriends.Add(new PlayerFriend { PlayerId = 2, FriendPlayerId = 1, Status = FriendStatus.Pending });
            db.SaveChanges();
        }

        var cut = ctx.Render<FriendsPage>();

        Assert.Contains("Requests (1)", cut.Markup);
    }
}
