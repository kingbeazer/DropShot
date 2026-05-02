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

    /// <summary>
    /// Server-resolves the state of <c>/invite/{token}</c> for the current
    /// user: invalid, already-accepted, anonymous (needs auth), self-invited,
    /// or ready-to-confirm. Returns <c>null</c> only on hard failure; the
    /// normal "not found" / "already accepted" paths surface via
    /// <see cref="InvitationViewStatus.Invalid"/> / <c>AlreadyAccepted</c>
    /// with an <c>ErrorMessage</c>.
    /// </summary>
    Task<InvitationViewDto> GetInvitationViewAsync(Guid token, CancellationToken ct = default);

    /// <summary>
    /// Accept the invitation: migrate <c>SavedMatch</c> player references
    /// from the light record to the current user's verified player, mark
    /// the invitation accepted, bookmark the merged player for the inviter,
    /// and delete the light row. Idempotency: returns
    /// <c>Success=false</c> with an explanation when the token is no longer
    /// valid (already accepted, light player removed, current user has no
    /// verified record).
    /// </summary>
    Task<AcceptInvitationResultDto> AcceptInvitationAsync(Guid token, CancellationToken ct = default);
}
