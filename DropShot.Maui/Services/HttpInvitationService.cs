using System.Net.Http.Json;
using DropShot.Shared.Dtos;
using DropShot.UI.Services;

namespace DropShot.Maui.Services;

/// <summary>
/// MAUI HTTP implementation of <see cref="IInvitationService"/>. Phase 5 —
/// light-player invite flow. Server builds the fully-qualified URL using
/// <c>App:BaseUrl</c>, so MAUI just renders what the API returns.
/// </summary>
public sealed class HttpInvitationService(HttpClient http) : IInvitationService
{
    public async Task<LightPlayerInvitationDto> CreateOrReuseLightPlayerInvitationAsync(
        int lightPlayerId, CancellationToken ct = default)
    {
        var resp = await http.PostAsync(
            $"api/invitations/light-player/{lightPlayerId}", null, ct);
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<LightPlayerInvitationDto>(cancellationToken: ct))!;
    }

    public async Task SendInvitationEmailAsync(Guid token, string email, CancellationToken ct = default)
    {
        var resp = await http.PostAsJsonAsync(
            $"api/invitations/{token}/send-email",
            new SendInvitationEmailRequest(email), ct);
        resp.EnsureSuccessStatusCode();
    }

    public async Task<InvitationViewDto> GetInvitationViewAsync(Guid token, CancellationToken ct = default) =>
        await http.GetFromJsonAsync<InvitationViewDto>($"api/invitations/{token}/view", ct)
        ?? new InvitationViewDto(InvitationViewStatus.Invalid,
            "Failed to load invitation.", null, 0, null);

    public async Task<AcceptInvitationResultDto> AcceptInvitationAsync(Guid token, CancellationToken ct = default)
    {
        var resp = await http.PostAsync($"api/invitations/{token}/accept", null, ct);
        if (!resp.IsSuccessStatusCode)
            return new AcceptInvitationResultDto(false, "Failed to accept invitation.");
        return (await resp.Content.ReadFromJsonAsync<AcceptInvitationResultDto>(cancellationToken: ct))
            ?? new AcceptInvitationResultDto(false, "Failed to accept invitation.");
    }
}
