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
    /// record exists for the user, or already-registered. The <paramref name="consent"/>
    /// payload carries the per-competition phone-share consent collected in
    /// the EnterCompetitionConsentDialog and is recorded against the player.
    /// </summary>
    Task SelfRegisterAsync(
        int competitionId, ParticipantStatus status, PhoneShareConsent consent, CancellationToken ct = default);

    /// <summary>
    /// Upgrade the authenticated user's participation status (typically from
    /// Registered → FullPlayer or Substitute). Requires a fresh consent
    /// payload — the user re-acknowledges phone-share visibility each time
    /// they (re-)commit to participating.
    /// </summary>
    Task ConfirmParticipationAsync(
        int competitionId, ParticipantStatus status, PhoneShareConsent consent, CancellationToken ct = default);

    /// <summary>
    /// Approve a fixture's awaiting-verification result. Without
    /// <c>OverrideScores</c> the existing submission is accepted as-is; with
    /// it, the original result is preserved for audit and the new score
    /// becomes authoritative. Marks Completed + runs CompetitionProgressionService.
    /// </summary>
    Task ApproveFixtureResultAsync(int fixtureId, ApproveFixtureResultRequest request, CancellationToken ct = default);

    /// <summary>
    /// SuperAdmin-only: generate synthetic activity for a SinglesLadder
    /// competition. Destructive — wipes prior fixtures + decay events and
    /// resets every participant before simulating. See
    /// <c>LadderSimulationService</c> for behaviour.
    /// </summary>
    Task<LadderSimulationResultDto> SimulateLadderAsync(int competitionId, int weeks, int? seed = null, CancellationToken ct = default);

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
    /// Loads the singles/doubles scoring context for the SubmitScorePage:
    /// the fixture DTO plus the competition's match-config knobs (MatchFormat,
    /// NumberOfSets, BestOf, GamesPerSet, SetWinMode) and a <c>CanAdminOverride</c>
    /// flag derived from the caller's permissions. Returns <c>null</c> when
    /// the fixture doesn't exist; throws <see cref="UnauthorizedAccessException"/>
    /// when the caller can neither view the competition nor is a participant
    /// in the fixture.
    /// </summary>
    Task<FixtureScoreContextDto?> GetFixtureScoreContextAsync(int fixtureId, CancellationToken ct = default);

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
    /// Upcoming fixtures (Scheduled or InProgress) the current user is in,
    /// either as a singles/doubles player or via a team they're a member of.
    /// Returns an empty list when no user is signed in or the user has no
    /// Player profile. Each fixture carries its <c>CompetitionName</c>.
    /// </summary>
    Task<List<CompetitionFixtureDto>> GetMyUpcomingFixturesAsync(CancellationToken ct = default);

    /// <summary>
    /// Most-recently completed fixtures (with a <c>ResultSummary</c>) the
    /// current user participated in, either directly or via a team. Returns
    /// an empty list when no user is signed in or the user has no Player
    /// profile. Each fixture carries its <c>CompetitionName</c>.
    /// </summary>
    Task<List<CompetitionFixtureDto>> GetMyRecentCompletedFixturesAsync(
        int limit = 6, CancellationToken ct = default);

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
    /// Self-enter the authenticated user's player into the competition with
    /// the chosen participation status (FullPlayer or Substitute). Server
    /// enforces date/eligibility/capacity/duplicate-entry guards and throws
    /// <see cref="InvalidOperationException"/> with a user-facing message
    /// when any of them fail, or when <paramref name="status"/> is not one
    /// of the two allowed values. The <paramref name="consent"/> payload is
    /// recorded against the player at the same time the participant row is
    /// created (single transaction).
    /// </summary>
    Task EnterCompetitionAsync(
        int competitionId,
        PhoneShareConsent consent,
        ParticipantStatus status = ParticipantStatus.FullPlayer,
        CancellationToken ct = default);

    /// <summary>
    /// Leave a competition. Sets the participant's status to
    /// <see cref="ParticipantStatus.Withdrawn"/> and stamps the active
    /// CompetitionEntryConsent row's WithdrawnUtc, dropping the player's
    /// number from peer views. One-click action — matches the GDPR
    /// principle that withdrawal must be as easy as consent.
    /// </summary>
    Task LeaveCompetitionAsync(int competitionId, CancellationToken ct = default);

    /// <summary>
    /// Loads the rubber-scoring context for a team-match fixture: per-rubber
    /// player names + existing set scores + score aggregates, plus the fixture's
    /// match-config knobs (BestOf, GamesPerSet, SetWinMode, MatchFormat,
    /// RequireVerification, LeagueScoring, HostClubId). Backs both rubber
    /// scoring dialogs and the TeamMatchScoring page.
    /// </summary>
    Task<FixtureRubberContextDto?> GetFixtureRubberContextAsync(int fixtureId, CancellationToken ct = default);

    /// <summary>
    /// Lazily creates the Rubber rows for a team-match fixture by resolving
    /// each rubber template's roles against the home and away team rosters.
    /// Idempotent (returns immediately if rubbers already exist). Throws
    /// <see cref="InvalidOperationException"/> with a user-facing message when
    /// role resolution fails (missing or duplicate role assignments) — the
    /// TeamMatchScoring page surfaces this as a recoverable error.
    /// </summary>
    Task EnsureFixtureRubbersAsync(int fixtureId, CancellationToken ct = default);

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

    /// <summary>
    /// Anonymous lookup for the <c>/verify-result/{token}</c> page.
    /// Resolves the fixture by VerificationToken only when its Status is
    /// AwaitingVerification — already-completed or no-longer-awaiting
    /// tokens return <c>null</c>. Pre-computes the side labels + aggregate
    /// for team matches and pre-parses set scores per rubber.
    /// </summary>
    Task<VerifyFixtureViewDto?> GetFixtureForVerificationAsync(Guid token, CancellationToken ct = default);

    /// <summary>
    /// Anonymous approval of an awaiting-verification fixture by token.
    /// Mirrors the inline VerifyResult logic: optional score override
    /// (singles/doubles, preserves the original for audit) or manual
    /// tie-break winner (team match). Sets Status=Completed, runs bracket
    /// progression, returns the competition id so the caller can deep-link
    /// back. Idempotent — returns <c>Success=false</c> with an explanation
    /// when the token is no longer valid.
    /// </summary>
    Task<ApproveFixtureByTokenResultDto> ApproveFixtureByTokenAsync(
        Guid token, ApproveFixtureByTokenRequest request, CancellationToken ct = default);
}
