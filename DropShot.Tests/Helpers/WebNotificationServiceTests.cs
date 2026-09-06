using DropShot.Data;
using DropShot.Models;
using DropShot.Services;
using DropShot.Shared;
using Xunit;

namespace DropShot.Tests.Helpers;

public class WebNotificationServiceTests
{
    private static WebNotificationService BuildService(TestDbContextFactory factory, string userId) =>
        new(factory, new FakeCurrentUser(userId), FakeHubContext.Create());

    private static async Task SeedUserAsync(TestDbContextFactory factory, string userId)
    {
        using var db = factory.CreateDbContext();
        db.Users.Add(new ApplicationUser { Id = userId, UserName = $"{userId}@example.com", Email = $"{userId}@example.com", DisplayName = userId });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task CreateAsync_ThenGetUnreadCount_ReturnsOne()
    {
        var factory = new TestDbContextFactory();
        await SeedUserAsync(factory, "user-a");
        var svc = BuildService(factory, "user-a");

        await svc.CreateAsync("user-a", NotificationType.MessageReceived, "New message");

        Assert.Equal(1, await svc.GetUnreadCountAsync());
    }

    [Fact]
    public async Task MarkAsReadAsync_DecrementsUnreadCount()
    {
        var factory = new TestDbContextFactory();
        await SeedUserAsync(factory, "user-a");
        var svc = BuildService(factory, "user-a");
        var created = await svc.CreateAsync("user-a", NotificationType.MessageReceived, "New message");

        await svc.MarkAsReadAsync(created.NotificationId);

        Assert.Equal(0, await svc.GetUnreadCountAsync());
    }

    [Fact]
    public async Task MarkAllAsReadAsync_ClearsAllUnread()
    {
        var factory = new TestDbContextFactory();
        await SeedUserAsync(factory, "user-a");
        var svc = BuildService(factory, "user-a");
        await svc.CreateAsync("user-a", NotificationType.MessageReceived, "First");
        await svc.CreateAsync("user-a", NotificationType.MessageRequestReceived, "Second");

        await svc.MarkAllAsReadAsync();

        Assert.Equal(0, await svc.GetUnreadCountAsync());
    }

    [Fact]
    public async Task GetUnreadCountAsync_ScopedToCurrentUser()
    {
        var factory = new TestDbContextFactory();
        await SeedUserAsync(factory, "user-a");
        await SeedUserAsync(factory, "user-b");
        var svcA = BuildService(factory, "user-a");
        var svcB = BuildService(factory, "user-b");

        await svcA.CreateAsync("user-a", NotificationType.MessageReceived, "For A");

        Assert.Equal(1, await svcA.GetUnreadCountAsync());
        Assert.Equal(0, await svcB.GetUnreadCountAsync());
    }
}
