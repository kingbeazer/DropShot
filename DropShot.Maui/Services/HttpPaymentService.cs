using System.Net.Http.Json;
using DropShot.Shared.Dtos;
using DropShot.UI.Services;

namespace DropShot.Maui.Services;

/// <summary>
/// MAUI HTTP implementation of <see cref="IPaymentService"/>. Reads
/// subscription status via the API. Activation is web-only (the PayPal SDK
/// flow runs in the browser); MAUI users tap "Subscribe via website" which
/// opens https://ds.tennis/upgrade in the system browser, complete payment
/// there, and refresh on return to see the activated state.
/// </summary>
public sealed class HttpPaymentService(HttpClient http) : IPaymentService
{
    public async Task<SubscriptionStatusDto> GetSubscriptionStatusAsync(CancellationToken ct = default) =>
        await http.GetFromJsonAsync<SubscriptionStatusDto>("api/subscription/status", ct)
            ?? new SubscriptionStatusDto(false, null, null);

    public Task ActivateSubscriptionAsync(ActivateSubscriptionRequest request, CancellationToken ct = default) =>
        throw new NotSupportedException(
            "Direct subscription activation is web-only. MAUI users complete the PayPal flow in the system browser.");
}
