using DropShot.Shared.Dtos;

namespace DropShot.UI.Services;

/// <summary>
/// Match-setup wizard abstraction. Phase 7 PR 7e: the wizard component moves
/// into the shared RCL together with TennisScore.razor; this surface wraps
/// the inline EF queries it currently does (my-light-players, bookmarked
/// players, club + court drilldown, fuzzy index seeding, auto-bookmark on
/// match start). Inline light-player creation reuses
/// <see cref="IPlayerService.CreateMyLightPlayerAsync"/>; court availability
/// for the live-scoring path stays on
/// <see cref="IMatchScoringService.GetAvailableCourtsAsync"/>.
/// </summary>
public interface IMatchSetupService
{
    /// <summary>
    /// One-shot payload for the wizard's <c>OnInitializedAsync</c>: the
    /// current user's Player profile (null when no current user or no
    /// linked Player), the user's light-player roster, the user's
    /// bookmarked verified players (excluding self), every club for the
    /// autocomplete, and the fuzzy-index seed. Anonymous-safe: returns
    /// null/empty payloads when no current user.
    /// </summary>
    Task<MatchSetupBootstrapDto> GetBootstrapAsync(CancellationToken ct = default);

    /// <summary>
    /// Courts for the selected club, ordered by name. Backs the wizard's
    /// club → court drilldown step.
    /// </summary>
    Task<List<WizardCourtDto>> GetCourtsByClubAsync(int clubId, CancellationToken ct = default);

    /// <summary>
    /// Idempotent batch-bookmark: insert UserPlayer rows for the current
    /// user against any of <paramref name="request"/>'s player ids that
    /// aren't already bookmarked. No-op when no current user. Called on
    /// "Start match" so any non-light, non-self players in the wizard get
    /// auto-saved to the user's "My Players" list.
    /// </summary>
    Task AutoBookmarkPlayersAsync(AutoBookmarkPlayersRequest request, CancellationToken ct = default);
}
