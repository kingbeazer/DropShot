using DropShot.Shared.Dtos;

namespace DropShot.UI.Services;

/// <summary>
/// Court-claim coordination: figuring out whether a SavedMatch on a court is
/// still active or abandoned, and acting on the result. Phase 7 surface —
/// covers what <c>ClaimCourtDialog</c> needs (stale check + force-end) so
/// the dialog can move into the RCL. The full evaluation surface
/// (<c>EvaluateAsync</c>, grace tracking) stays web-only for now since its
/// only caller is <c>TennisScore.razor</c>.
/// </summary>
public interface ICourtClaimService
{
    /// <summary>True when the SavedMatch hasn't seen activity for the abandon threshold.</summary>
    Task<bool> IsStaleAsync(int savedMatchId, CancellationToken ct = default);

    /// <summary>
    /// Extend the grace window after the occupant says "yes, I'm still playing"
    /// in response to a court-challenge. Pushes <c>ClaimGraceUntilUtc</c>
    /// forward and refreshes <c>LastActivityAt</c> so the abandon timer
    /// resets. PR 7b's TennisScore move calls this from the CourtChallenge
    /// SignalR handler.
    /// </summary>
    Task ExtendGraceAsync(int savedMatchId, CancellationToken ct = default);

    /// <summary>Mark the SavedMatch complete and clear the grace window.</summary>
    Task EndMatchAsync(int savedMatchId, CancellationToken ct = default);

    /// <summary>
    /// Most recent incomplete <c>SavedMatch</c> owned by <paramref name="userId"/>
    /// so the "Play" buttons on Home and TennisScore can offer to resume or end
    /// it before starting a new one. Pass <paramref name="excludingSavedMatchId"/>
    /// to ignore a known-current match (e.g. the one being scored on the page
    /// that's calling). Returns <c>null</c> when no active match exists or the
    /// caller is anonymous.
    /// </summary>
    Task<ActiveMatchDto?> GetUserActiveMatchAsync(
        string userId, int? excludingSavedMatchId = null, CancellationToken ct = default);
}
