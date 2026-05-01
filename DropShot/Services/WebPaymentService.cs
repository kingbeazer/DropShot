using DropShot.Data;
using DropShot.Shared.Dtos;
using DropShot.UI.Services;
using DropShot.UI.Services.Auth;
using Microsoft.EntityFrameworkCore;

namespace DropShot.Services;

/// <summary>
/// Web implementation of <see cref="IPaymentService"/>. Reads the current
/// user's subscription state from the AspNetUsers table and stamps the
/// activation fields when the PayPal SDK approves on the web. The full
/// PayPal REST verification (validating subscriptionId against PayPal's API)
/// stays as a TODO for the existing flow — this service mirrors the in-page
/// activation that worked before the migration.
/// </summary>
public sealed class WebPaymentService(
    IDbContextFactory<MyDbContext> dbFactory,
    ICurrentUser currentUser) : IPaymentService
{
    public async Task<SubscriptionStatusDto> GetSubscriptionStatusAsync(CancellationToken ct = default)
    {
        var userId = currentUser.UserId;
        if (string.IsNullOrEmpty(userId))
            return new SubscriptionStatusDto(false, null, null);

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var user = await db.Users.FindAsync([userId], ct);
        if (user is null) return new SubscriptionStatusDto(false, null, null);
        return new SubscriptionStatusDto(user.IsSubscribed, user.SubscriptionTier, user.SubscriptionStartDate);
    }

    public async Task ActivateSubscriptionAsync(ActivateSubscriptionRequest request, CancellationToken ct = default)
    {
        var userId = currentUser.UserId
            ?? throw new InvalidOperationException("Not authenticated.");

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var user = await db.Users.FindAsync([userId], ct)
            ?? throw new KeyNotFoundException("User not found.");

        user.IsSubscribed = true;
        user.SubscriptionTier = "Premium";
        user.SubscriptionStartDate = DateTime.UtcNow;
        user.PaypalSubscriptionId = request.SubscriptionId;
        user.PaypalPayerId = request.PayerId;
        await db.SaveChangesAsync(ct);
    }
}
