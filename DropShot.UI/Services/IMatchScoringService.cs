using DropShot.Shared.Dtos;

namespace DropShot.UI.Services;

/// <summary>
/// Live-scoring abstraction for <c>TennisScore.razor</c>. Phase 7 prep PR:
/// adds the database/service surface the live-scoring page needs so the
/// <c>.razor</c> file can move into the shared RCL without injecting
/// <c>IDbContextFactory</c> directly. The existing rubber finalisation
/// cascade lives on <see cref="ICompetitionService.SubmitRubberScoresAsync"/>
/// and is reused as-is; only the singles cascade and the page-specific
/// bootstrap loads + <c>SavedMatch</c> upsert need a new surface.
/// </summary>
public interface IMatchScoringService
{
    /// <summary>
    /// One-shot bootstrap for the <c>OnInitializedAsync</c> path: returns
    /// the user's preferred game-scoring (null when no current user or no
    /// linked Player), the user's PlayerId, every player with their
    /// linked-account avatar (drives the autocomplete dropdown), and the
    /// user's accepted-friend player ids (drives the friends-first sort).
    /// Anonymous-safe: returns nullable preference fields and an empty
    /// friend list when no current user.
    /// </summary>
    Task<TennisScoreBootstrapDto> GetBootstrapAsync(CancellationToken ct = default);

    /// <summary>
    /// Loads an in-progress <c>SavedMatch</c> by id for the <c>/match/{id}</c>
    /// resume route. Returns <c>null</c> when not found, already complete, or
    /// the caller doesn't own the match (UserId / DeviceToken mismatch). The
    /// linked competition fixture (if any) is included so fixture chrome
    /// renders without a second round-trip.
    /// </summary>
    Task<SavedMatchResumeDto?> GetSavedMatchForResumeAsync(int savedMatchId, CancellationToken ct = default);

    /// <summary>
    /// Loads a <see cref="TennisScoreFixtureContextDto"/> for the
    /// <c>?fixtureId=...</c> "start a fresh match from a fixture" entry path.
    /// Returns <c>null</c> when the fixture doesn't exist.
    /// </summary>
    Task<TennisScoreFixtureContextDto?> GetFixtureContextAsync(int fixtureId, CancellationToken ct = default);

    /// <summary>
    /// Loads a <see cref="TennisScoreRubberContextDto"/> for the
    /// <c>?rubberId=...</c> "start a rubber from TeamMatchScoring" entry
    /// path. Returns <c>null</c> when the rubber doesn't exist.
    /// </summary>
    Task<TennisScoreRubberContextDto?> GetRubberContextAsync(int rubberId, CancellationToken ct = default);

    /// <summary>
    /// Returns courts available for selection on the live-scoring page:
    /// every court with no in-progress <c>SavedMatch</c>, plus the
    /// caller-supplied <c>selectedCourtId</c> (if any) so the page's
    /// previously-saved court stays selectable even when occupied.
    /// </summary>
    Task<List<ScoringCourtDto>> GetAvailableCourtsAsync(int? selectedCourtId, CancellationToken ct = default);

    /// <summary>
    /// Persists the current user's preferred <c>DefaultGameScoring</c> on
    /// their <c>Player</c> row. No-op when no current user or no linked
    /// Player record.
    /// </summary>
    Task SavePreferredGameScoringAsync(bool gameScoring, CancellationToken ct = default);

    /// <summary>
    /// Idempotent friend request: creates a <c>Pending</c> <c>PlayerFriend</c>
    /// from the current user's player to <paramref name="targetPlayerId"/>,
    /// unless one already exists in either direction. No-op when no current
    /// user or the current user has no Player record.
    /// </summary>
    Task SendFriendRequestAsync(int targetPlayerId, CancellationToken ct = default);

    /// <summary>
    /// Upsert the live-scoring <c>SavedMatch</c> row. On first call
    /// (<c>SavedMatchId</c> null) the server allocates a new row keyed to the
    /// current user, or to the caller-supplied <c>DeviceToken</c> when
    /// anonymous; subsequent calls update the same row. When
    /// <c>LinkedFixtureId</c> is set and the fixture isn't yet linked, the
    /// server sets fixture <c>SavedMatchId</c> + <c>Status = InProgress</c>
    /// in the same transaction. Returns the (possibly newly allocated)
    /// <c>SavedMatchId</c>.
    /// </summary>
    Task<int> UpsertLiveMatchAsync(UpsertLiveMatchRequest request, CancellationToken ct = default);

    /// <summary>
    /// Singles end-of-match cascade for the live-scoring path: sets fixture
    /// <c>Status = Completed</c>, fills aggregates, links <c>SavedMatchId</c>,
    /// runs <c>CompetitionProgressionService.TryAdvanceAsync</c>. No-op when
    /// the fixture is already <c>Completed</c>. Distinct from
    /// <see cref="ICompetitionService.SubmitFixtureScoreAsync"/> — the live
    /// path always finalises directly (no AwaitingVerification + email
    /// cascade), preserving the inline behaviour TennisScore.razor has
    /// today.
    /// </summary>
    Task FinaliseLiveFixtureAsync(int fixtureId, FinaliseLiveFixtureRequest request, CancellationToken ct = default);

    /// <summary>
    /// Discard an in-flight live-scoring match: deletes the <c>SavedMatch</c>
    /// row and resets any linked fixture (Status=Scheduled, clear winner /
    /// summary / SavedMatchId) and rubber (IsComplete=false, clear winner /
    /// games / SavedMatchId). Idempotent. Anonymous-safe when caller's
    /// <c>DeviceToken</c> matches the row's.
    /// </summary>
    Task DiscardLiveMatchAsync(int savedMatchId, string? deviceToken, CancellationToken ct = default);

    /// <summary>
    /// Ad-hoc fixture creation for a Singles Ladder competition. Resolves the
    /// caller's Player from JWT, validates both caller and opponent are
    /// FullPlayer participants in the competition, creates a Scheduled
    /// fixture (Player1 = caller, Player2 = opponent), and returns its id so
    /// the caller can navigate to <c>/tennisscore?fixtureId=…</c>. Throws
    /// <see cref="UnauthorizedAccessException"/> when caller has no linked
    /// Player or when either side isn't a FullPlayer participant.
    /// </summary>
    Task<int> CreateLadderFixtureAsync(CreateLadderFixtureRequest request, CancellationToken ct = default);
}
