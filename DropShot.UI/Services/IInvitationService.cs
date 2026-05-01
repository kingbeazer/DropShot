namespace DropShot.UI.Services;

/// <summary>
/// Invitation domain abstraction. Marker interface at phase 3 — populated in
/// phases 4–5 alongside Invite, MobileAuth, and VerifyResult. Required
/// endpoints (<c>POST /api/players/{id}/link-request</c>, the
/// <c>/api/invitations</c> family) land per the API gap log.
/// </summary>
public interface IInvitationService
{
}
