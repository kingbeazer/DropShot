using DropShot.Data;
using DropShot.Models;
using DropShot.Services;
using DropShot.Shared;
using Xunit;

namespace DropShot.Tests.Helpers;

public class WebFriendServiceTests
{
    private static WebFriendService BuildService(TestDbContextFactory factory, string userId) =>
        new(factory, new FakeCurrentUser(userId));

    private static async Task SeedPlayersAsync(TestDbContextFactory factory, string userAId, int playerAId, string userBId, int playerBId)
    {
        using var db = factory.CreateDbContext();
        db.Users.Add(new ApplicationUser { Id = userAId, UserName = "a@example.com", Email = "a@example.com", DisplayName = "Alice" });
        db.Users.Add(new ApplicationUser { Id = userBId, UserName = "b@example.com", Email = "b@example.com", DisplayName = "Bob" });
        db.Players.Add(new Player { PlayerId = playerAId, DisplayName = "Alice", UserId = userAId });
        db.Players.Add(new Player { PlayerId = playerBId, DisplayName = "Bob", UserId = userBId });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task AcceptFriendRequestAsync_FlipsStatusToAccepted()
    {
        var factory = new TestDbContextFactory();
        await SeedPlayersAsync(factory, "user-a", 1, "user-b", 2);
        using (var db = factory.CreateDbContext())
        {
            db.PlayerFriends.Add(new PlayerFriend { PlayerId = 1, FriendPlayerId = 2, Status = FriendStatus.Pending });
            await db.SaveChangesAsync();
        }

        // Bob (recipient) accepts Alice's (player 1) request.
        var svc = BuildService(factory, "user-b");
        await svc.AcceptFriendRequestAsync(requestingPlayerId: 1);

        using var verify = factory.CreateDbContext();
        var row = Assert.Single(verify.PlayerFriends);
        Assert.Equal(FriendStatus.Accepted, row.Status);
    }

    [Fact]
    public async Task RejectFriendRequestAsync_RemovesRow()
    {
        var factory = new TestDbContextFactory();
        await SeedPlayersAsync(factory, "user-a", 1, "user-b", 2);
        using (var db = factory.CreateDbContext())
        {
            db.PlayerFriends.Add(new PlayerFriend { PlayerId = 1, FriendPlayerId = 2, Status = FriendStatus.Pending });
            await db.SaveChangesAsync();
        }

        var svc = BuildService(factory, "user-b");
        await svc.RejectFriendRequestAsync(requestingPlayerId: 1);

        using var verify = factory.CreateDbContext();
        Assert.Empty(verify.PlayerFriends);
    }

    [Fact]
    public async Task AreUsersFriendsAsync_TrueOnlyWhenAccepted()
    {
        var factory = new TestDbContextFactory();
        await SeedPlayersAsync(factory, "user-a", 1, "user-b", 2);
        using (var db = factory.CreateDbContext())
        {
            db.PlayerFriends.Add(new PlayerFriend { PlayerId = 1, FriendPlayerId = 2, Status = FriendStatus.Pending });
            await db.SaveChangesAsync();
        }

        var svc = BuildService(factory, "user-a");
        Assert.False(await svc.AreUsersFriendsAsync("user-a", "user-b"));

        using (var db = factory.CreateDbContext())
        {
            var row = db.PlayerFriends.Single();
            row.Status = FriendStatus.Accepted;
            await db.SaveChangesAsync();
        }

        Assert.True(await svc.AreUsersFriendsAsync("user-a", "user-b"));
    }
}
