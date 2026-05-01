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

    /// <summary>Mark the SavedMatch complete and clear the grace window.</summary>
    Task EndMatchAsync(int savedMatchId, CancellationToken ct = default);
}
