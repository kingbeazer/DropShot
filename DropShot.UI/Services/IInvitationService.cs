using DropShot.Shared.Dtos;

namespace DropShot.UI.Services;

/// <summary>
/// Invitation domain abstraction. Phase 5 light-player surface; Invite +
/// MobileAuth + VerifyResult flows extend this in later phases.
/// </summary>
public interface IInvitationService
{
    /// <summary>
    /// Reuse the most recent unaccepted invitation for the given light player
    /// (created by the current user) or create a new one. Returns the token
    /// and a fully-qualified invite URL the host can render in a copy field.
    /// </summary>
    Task<LightPlayerInvitationDto> CreateOrReuseLightPlayerInvitationAsync(int lightPlayerId, CancellationToken ct = default);

    /// <summary>Send the invitation email server-side using the invitation token.</summary>
    Task SendInvitationEmailAsync(Guid token, string email, CancellationToken ct = default);
}
