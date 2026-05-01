using DropShot.Shared;
using DropShot.Shared.Dtos;

namespace DropShot.UI.Services;

/// <summary>
/// Competition domain abstraction. Seeded with the read surface needed by
/// ViewCompetition, LeagueTable, and DisplayControl (phase 4); setup/scoring
/// write methods land in phases 5–7.
/// </summary>
public interface ICompetitionService
{
    Task<List<CompetitionDto>> GetCompetitionsAsync(bool includeArchived = false, CancellationToken ct = default);
    Task<CompetitionDetailDto?> GetCompetitionAsync(int id, CancellationToken ct = default);

    /// <summary>
    /// Register the authenticated user as a participant in the competition.
    /// Server resolves the player from the authenticated user's <c>UserId</c>;
    /// returns a 400-equivalent on web (KeyNotFoundException) when no player
    /// record exists for the user, or already-registered.
    /// </summary>
    Task SelfRegisterAsync(int competitionId, ParticipantStatus status, CancellationToken ct = default);

    /// <summary>
    /// Upgrade the authenticated user's participation status (typically from
    /// Registered → FullPlayer or Substitute).
    /// </summary>
    Task ConfirmParticipationAsync(int competitionId, ParticipantStatus status, CancellationToken ct = default);

    /// <summary>
    /// Approve a fixture's awaiting-verification result. Without
    /// <c>OverrideScores</c> the existing submission is accepted as-is; with
    /// it, the original result is preserved for audit and the new score
    /// becomes authoritative. Marks Completed + runs CompetitionProgressionService.
    /// </summary>
    Task ApproveFixtureResultAsync(int fixtureId, ApproveFixtureResultRequest request, CancellationToken ct = default);

    /// <summary>
    /// Submit a fixture score for the first time (or override an existing one
    /// when the caller is an admin). Server enforces RequireVerification
    /// (sets Status = AwaitingVerification + a VerificationToken when needed)
    /// and runs CompetitionProgressionService.TryAdvanceAsync when the
    /// result is final. Notification / verification emails fire in the
    /// background so the caller returns immediately.
    /// </summary>
    Task SubmitFixtureScoreAsync(int fixtureId, SubmitFixtureScoreRequest request, CancellationToken ct = default);

    /// <summary>
    /// Returns the user-view payload for the "/competitions" page: the list of
    /// competitions the authenticated user has entered, plus the eligible
    /// not-yet-entered competitions still open for registration. Server applies
    /// the same eligibility/date/restriction guard <see cref="EnterCompetitionAsync"/>
    /// uses on submit.
    /// </summary>
    Task<MyCompetitionsViewDto> GetMyCompetitionsViewAsync(CancellationToken ct = default);

    /// <summary>
    /// Returns awaiting-verification fixtures the caller is allowed to review
    /// (system admin → all, club admin → host-club's, competition admin → owned).
    /// Each fixture carries its <c>CompetitionName</c> for grouping in the UI.
    /// </summary>
    Task<List<CompetitionFixtureDto>> GetPendingVerificationFixturesAsync(CancellationToken ct = default);

    /// <summary>
    /// Toggle the IsArchived flag on a competition. Caller must be able to
    /// edit the competition; the server performs the final auth check.
    /// </summary>
    Task ToggleArchiveAsync(int competitionId, CancellationToken ct = default);

    /// <summary>
    /// Permanently delete a competition and its child rows (rubbers, fixtures).
    /// Caller must be able to edit the competition.
    /// </summary>
    Task DeleteCompetitionAsync(int competitionId, CancellationToken ct = default);

    /// <summary>
    /// Self-enter the authenticated user's player into the competition.
    /// Server enforces date/eligibility/capacity/duplicate-entry guards and
    /// throws <see cref="InvalidOperationException"/> with a user-facing
    /// message when any of them fail.
    /// </summary>
    Task EnterCompetitionAsync(int competitionId, CancellationToken ct = default);

    /// <summary>
    /// Loads the rubber-scoring context for a team-match fixture: per-rubber
    /// player names + existing set scores, plus the fixture's match-config
    /// knobs (BestOf, GamesPerSet, SetWinMode, MatchFormat, RequireVerification).
    /// Backs RubberScoreDialog and BulkRubberScoreDialog (phase 7).
    /// </summary>
    Task<FixtureRubberContextDto?> GetFixtureRubberContextAsync(int fixtureId, CancellationToken ct = default);

    /// <summary>
    /// Submit one or many rubber scores for a fixture in a single call. The
    /// single-rubber dialog sends one entry; the "enter all" dialog sends
    /// every rubber. Server persists each rubber, then if every rubber is
    /// complete runs the same finalisation cascade as the live-scoring path:
    /// score / tie-break resolution, notification or verification emails,
    /// and bracket progression. <c>AdminOverride = true</c> bypasses
    /// RequireVerification and (for an already-finalised fixture) updates
    /// aggregates in place without resetting verification tokens or resending
    /// emails.
    /// </summary>
    Task SubmitRubberScoresAsync(int fixtureId, SubmitRubberScoresRequest request, CancellationToken ct = default);
}
