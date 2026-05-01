namespace DropShot.Shared.Dtos;

/// <summary>
/// Subscription status for the current user. Backs the Upgrade page's
/// "already subscribed" short-circuit.
/// </summary>
public record SubscriptionStatusDto(
    bool IsSubscribed,
    string? Tier,
    DateTime? StartDate);

/// <summary>
/// Web-flow PayPal subscription activation. The browser-side PayPal SDK
/// returns the approved subscriptionId + payerId; the server resolves them
/// against PayPal's API and stamps <c>ApplicationUser.IsSubscribed</c>.
/// </summary>
public record ActivateSubscriptionRequest(string SubscriptionId, string PayerId);
