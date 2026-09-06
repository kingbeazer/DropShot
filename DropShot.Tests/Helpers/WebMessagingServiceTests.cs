using DropShot.Data;
using DropShot.Models;
using DropShot.Services;
using DropShot.Shared;
using Xunit;

namespace DropShot.Tests.Helpers;

public class WebMessagingServiceTests
{
    private static (WebMessagingService svc, TestDbContextFactory factory) BuildService(string userId, TestDbContextFactory? factory = null)
    {
        factory ??= new TestDbContextFactory();
        var currentUser = new FakeCurrentUser(userId);
        var friendService = new WebFriendService(factory, currentUser);
        var notificationService = new WebNotificationService(factory, currentUser, FakeHubContext.Create());
        var svc = new WebMessagingService(factory, currentUser, friendService, notificationService, FakeHubContext.Create());
        return (svc, factory);
    }

    private static async Task SeedUsersAsync(TestDbContextFactory factory, params string[] userIds)
    {
        using var db = factory.CreateDbContext();
        foreach (var id in userIds)
            db.Users.Add(new ApplicationUser { Id = id, UserName = $"{id}@example.com", Email = $"{id}@example.com", DisplayName = id });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task StartOrSendAsync_NotFriends_CreatesPendingApproval()
    {
        var factory = new TestDbContextFactory();
        await SeedUsersAsync(factory, "user-a", "user-b");
        var (svc, _) = BuildService("user-a", factory);

        var result = await svc.StartOrSendAsync("user-b", "Hello there");

        Assert.Equal(ConversationStatus.PendingApproval, result.Status);
    }

    [Fact]
    public async Task StartOrSendAsync_Friends_CreatesActive()
    {
        var factory = new TestDbContextFactory();
        await SeedUsersAsync(factory, "user-a", "user-b");
        using (var db = factory.CreateDbContext())
        {
            db.Players.Add(new Player { PlayerId = 1, DisplayName = "A", UserId = "user-a" });
            db.Players.Add(new Player { PlayerId = 2, DisplayName = "B", UserId = "user-b" });
            db.PlayerFriends.Add(new PlayerFriend { PlayerId = 1, FriendPlayerId = 2, Status = FriendStatus.Accepted });
            await db.SaveChangesAsync();
        }
        var (svc, _) = BuildService("user-a", factory);

        var result = await svc.StartOrSendAsync("user-b", "Hello friend");

        Assert.Equal(ConversationStatus.Active, result.Status);
    }

    [Fact]
    public async Task StartOrSendAsync_WhilePending_Throws()
    {
        var factory = new TestDbContextFactory();
        await SeedUsersAsync(factory, "user-a", "user-b");
        var (svc, _) = BuildService("user-a", factory);
        await svc.StartOrSendAsync("user-b", "First message");

        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.StartOrSendAsync("user-b", "Second message"));
    }

    [Fact]
    public async Task StartOrSendAsync_RecentlyDenied_Throws()
    {
        var factory = new TestDbContextFactory();
        await SeedUsersAsync(factory, "user-a", "user-b");
        var (senderSvc, _) = BuildService("user-a", factory);
        var (recipientSvc, _) = BuildService("user-b", factory);

        var result = await senderSvc.StartOrSendAsync("user-b", "Hi");
        await recipientSvc.DenyRequestAsync(result.ConversationId);

        await Assert.ThrowsAsync<InvalidOperationException>(() => senderSvc.StartOrSendAsync("user-b", "Again?"));
    }

    [Fact]
    public async Task ApproveRequestAsync_ByRecipient_MakesConversationActive()
    {
        var factory = new TestDbContextFactory();
        await SeedUsersAsync(factory, "user-a", "user-b");
        var (senderSvc, _) = BuildService("user-a", factory);
        var (recipientSvc, _) = BuildService("user-b", factory);

        var result = await senderSvc.StartOrSendAsync("user-b", "Hi");
        await recipientSvc.ApproveRequestAsync(result.ConversationId);

        using var verify = factory.CreateDbContext();
        var conversation = verify.Conversations.Single(c => c.ConversationId == result.ConversationId);
        Assert.Equal(ConversationStatus.Active, conversation.Status);
    }

    [Fact]
    public async Task ApproveRequestAsync_ByRequesterThemself_IsNoOp()
    {
        var factory = new TestDbContextFactory();
        await SeedUsersAsync(factory, "user-a", "user-b");
        var (senderSvc, _) = BuildService("user-a", factory);

        var result = await senderSvc.StartOrSendAsync("user-b", "Hi");
        // The requester can't approve their own request.
        await senderSvc.ApproveRequestAsync(result.ConversationId);

        using var verify = factory.CreateDbContext();
        var conversation = verify.Conversations.Single(c => c.ConversationId == result.ConversationId);
        Assert.Equal(ConversationStatus.PendingApproval, conversation.Status);
    }

    [Fact]
    public async Task GetMessagesAsync_HidesBodyFromRecipientUntilApproved()
    {
        var factory = new TestDbContextFactory();
        await SeedUsersAsync(factory, "user-a", "user-b");
        var (senderSvc, _) = BuildService("user-a", factory);
        var (recipientSvc, _) = BuildService("user-b", factory);

        var result = await senderSvc.StartOrSendAsync("user-b", "Secret first message");

        var recipientView = await recipientSvc.GetMessagesAsync(result.ConversationId, null, 50);
        Assert.Empty(recipientView);

        var senderView = await senderSvc.GetMessagesAsync(result.ConversationId, null, 50);
        Assert.Single(senderView);
    }

    [Fact]
    public async Task SendMessageAsync_OnlyWorksWhenActive()
    {
        var factory = new TestDbContextFactory();
        await SeedUsersAsync(factory, "user-a", "user-b");
        var (senderSvc, _) = BuildService("user-a", factory);

        var result = await senderSvc.StartOrSendAsync("user-b", "Hi");
        // Conversation is still PendingApproval — SendMessageAsync should be a no-op.
        await senderSvc.SendMessageAsync(result.ConversationId, "Ignored");

        using var verify = factory.CreateDbContext();
        Assert.Single(verify.Messages);
    }
}
