using Bunit;
using DropShot.Data;
using DropShot.Models;
using DropShot.Shared;
using DropShot.Tests.Helpers;
using DropShot.UI.Components.Pages;
using Xunit;

namespace DropShot.Tests.Pages;

public class MessagesPageTests
{
    [Fact]
    public async Task MessagesPage_Renders_Empty_State()
    {
        await using var ctx = new DropShotTestContext(authenticated: true, userId: "user-a");
        using (var db = ctx.SeedDatabase())
        {
            db.Users.Add(new ApplicationUser { Id = "user-a", UserName = "a@example.com", Email = "a@example.com", DisplayName = "Alice" });
            db.SaveChanges();
        }

        var cut = ctx.Render<MessagesPage>();

        Assert.Contains("No conversations yet", cut.Markup);
    }

    [Fact]
    public async Task MessagesPage_Shows_Active_Conversation()
    {
        await using var ctx = new DropShotTestContext(authenticated: true, userId: "user-a");
        using (var db = ctx.SeedDatabase())
        {
            db.Users.Add(new ApplicationUser { Id = "user-a", UserName = "a@example.com", Email = "a@example.com", DisplayName = "Alice" });
            db.Users.Add(new ApplicationUser { Id = "user-b", UserName = "b@example.com", Email = "b@example.com", DisplayName = "Bob" });
            var conversation = new Conversation
            {
                ConversationId = 1,
                UserAId = "user-a",
                UserBId = "user-b",
                RequestedByUserId = "user-a",
                Status = ConversationStatus.Active,
                CreatedAt = DateTime.UtcNow,
                LastMessageAt = DateTime.UtcNow
            };
            db.Conversations.Add(conversation);
            db.Messages.Add(new Message { ConversationId = 1, SenderUserId = "user-a", Body = "Hi Bob", SentAt = DateTime.UtcNow });
            db.SaveChanges();
        }

        var cut = ctx.Render<MessagesPage>();

        Assert.Contains("Bob", cut.Markup);
    }

    [Fact]
    public async Task MessagesPage_Shows_Incoming_Request_Without_Body()
    {
        await using var ctx = new DropShotTestContext(authenticated: true, userId: "user-b");
        using (var db = ctx.SeedDatabase())
        {
            db.Users.Add(new ApplicationUser { Id = "user-a", UserName = "a@example.com", Email = "a@example.com", DisplayName = "Alice" });
            db.Users.Add(new ApplicationUser { Id = "user-b", UserName = "b@example.com", Email = "b@example.com", DisplayName = "Bob" });
            var conversation = new Conversation
            {
                ConversationId = 1,
                UserAId = "user-a",
                UserBId = "user-b",
                RequestedByUserId = "user-a",
                Status = ConversationStatus.PendingApproval,
                CreatedAt = DateTime.UtcNow
            };
            db.Conversations.Add(conversation);
            db.Messages.Add(new Message { ConversationId = 1, SenderUserId = "user-a", Body = "Secret opener text", SentAt = DateTime.UtcNow });
            db.SaveChanges();
        }

        var cut = ctx.Render<MessagesPage>();

        // The Requests tab isn't the active panel by default — MudTabs only
        // renders the active panel's content — so switch to it first.
        cut.FindAll("div.mud-tab").First(e => e.TextContent.Contains("Requests")).Click();

        // Requests tab shows the requester's name but never the message body.
        Assert.Contains("Alice", cut.Markup);
        Assert.DoesNotContain("Secret opener text", cut.Markup);
    }
}
