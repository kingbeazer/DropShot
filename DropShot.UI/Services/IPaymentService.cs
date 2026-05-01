using DropShot.Shared.Dtos;

namespace DropShot.UI.Services;

/// <summary>
/// Subscription / payment domain abstraction. Phase 7 first surface — the
/// Upgrade page's read-side ("am I already subscribed?") and the activation
/// callback the web PayPal SDK invokes after approval. The MAUI subscription
/// flow itself (browser handoff to <c>/upgrade</c>, webhook activation) lives
/// outside this interface — MAUI just opens the URL via
/// <c>Browser.Default.OpenAsync</c> in the host shim.
/// </summary>
public interface IPaymentService
{
    Task<SubscriptionStatusDto> GetSubscriptionStatusAsync(CancellationToken ct = default);

    /// <summary>
    /// Activate a Premium subscription for the current user. Called from the
    /// web PayPal SDK's <c>onApprove</c> callback with the subscriptionId +
    /// payerId; throws when not authenticated or when the IDs don't validate.
    /// </summary>
    Task ActivateSubscriptionAsync(ActivateSubscriptionRequest request, CancellationToken ct = default);
}
