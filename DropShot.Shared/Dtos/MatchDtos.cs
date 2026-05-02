namespace DropShot.Shared.Dtos;

/// <summary>
/// Row in the Match landing page's "Active Matches" list. Backs the
/// non-admin "if they have an active match, go straight to it" shortcut and
/// the admin's match-list rendering.
/// </summary>
public record ActiveMatchDto(
    int SavedMatchId,
    string? Player1,
    string? Player2,
    string? Player3,
    string? Player4,
    int? CourtId,
    string? CourtName,
    DateTime CreatedAt);

/// <summary>
/// One set's game count for a casual match. May represent the only
/// set when the match was scored in game-only mode (no per-set
/// breakdown was recorded), in which case <see cref="SetNumber"/> == 1.
/// </summary>
public record CasualSetScoreDto(int SetNumber, int Player1Games, int Player2Games);

/// <summary>
/// Recently completed casual match (a SavedMatch row not linked to a
/// CompetitionFixture). Backs Home.razor's "My Recent Results" list.
/// Sets are pre-parsed from MatchJson server-side so the shared RCL
/// never deserializes the Match graph.
/// </summary>
public record RecentCasualMatchDto(
    int SavedMatchId,
    string? Player1,
    string? Player2,
    string? Player3,
    string? Player4,
    int? Player1Id,
    int? Player2Id,
    int? Player3Id,
    int? Player4Id,
    string? WinnerName,
    int? WinnerPlayerId,
    DateTime CreatedAt,
    DateTime? CompletedAt,
    IReadOnlyList<CasualSetScoreDto> Sets);

// ── Phase 7 PR 7d: IMatchScoringService surface (prep for TennisScore.razor move) ──

/// <summary>
/// One-shot bootstrap payload for the live-scoring page. Returns the current
/// user's preferred game-scoring flag (null when no current user or no Player
/// record), the user's PlayerId, the full player list with linked-account
/// avatar paths for the autocomplete, and the current user's accepted-friend
/// player ids (drives the friends-first sort in the dropdown).
/// </summary>
public record TennisScoreBootstrapDto(
    bool? PreferredGameScoring,
    int? MyPlayerId,
    IReadOnlyList<ScoringPlayerDto> Players,
    IReadOnlyList<int> FriendPlayerIds);

/// <summary>
/// Player row for the TennisScore autocomplete. <c>AvatarPath</c> is the
/// linked ApplicationUser's <c>ProfileImagePath</c> (not the player's own
/// avatar field, which is unused on this surface).
/// </summary>
public record ScoringPlayerDto(int PlayerId, string DisplayName, string? AvatarPath);

/// <summary>
/// Resume payload for an in-progress <c>SavedMatch</c> loaded from the
/// <c>/match/{id}</c> route. Carries the <c>MatchJson</c> blob the caller
/// deserialises into the in-memory <c>Match</c> graph plus the linked
/// fixture (when any) so the page can render fixture chrome on resume.
/// </summary>
public record SavedMatchResumeDto(
    int SavedMatchId,
    string MatchJson,
    int? CourtId,
    int? Player1Id,
    int? Player2Id,
    int? Player3Id,
    int? Player4Id,
    DateTime CreatedAt,
    TennisScoreFixtureContextDto? LinkedFixture);

/// <summary>
/// Lightweight projection of a <c>CompetitionFixture</c> for the live-scoring
/// page. Includes the player names + ids needed to seed the wizard, the
/// competition's match-config knobs needed for AutoStart, and the fixture
/// status used when relinking on save.
/// </summary>
public record TennisScoreFixtureContextDto(
    int CompetitionFixtureId,
    int CompetitionId,
    string? CompetitionName,
    string? StageName,
    string? FixtureLabel,
    int? CourtId,
    int? Player1Id, string? Player1Name,
    int? Player2Id, string? Player2Name,
    int? Player3Id, string? Player3Name,
    int? Player4Id, string? Player4Name,
    FixtureStatus Status,
    MatchFormatType MatchFormat,
    int BestOf,
    int NumberOfSets,
    int GamesPerSet,
    SetWinMode SetWinMode);

/// <summary>
/// Lightweight projection of a <c>Rubber</c> for the live-scoring page.
/// Carries the four player names + ids, the team labels, and the parent
/// competition's match-config knobs (rubbers inherit BestOf/format from the
/// competition).
/// </summary>
public record TennisScoreRubberContextDto(
    int RubberId,
    int CompetitionFixtureId,
    int CompetitionId,
    int? HomePlayer1Id, string? HomePlayer1Name,
    int? HomePlayer2Id, string? HomePlayer2Name,
    int? AwayPlayer1Id, string? AwayPlayer1Name,
    int? AwayPlayer2Id, string? AwayPlayer2Name,
    string? HomeTeamName,
    string? AwayTeamName,
    string? CompetitionName,
    MatchFormatType MatchFormat,
    int BestOf,
    int NumberOfSets,
    int GamesPerSet,
    SetWinMode SetWinMode);

/// <summary>
/// Court row for the in-match court selector. Server filters to courts that
/// either match the caller-supplied <c>selectedCourtId</c> or have no
/// in-progress <c>SavedMatch</c>.
/// </summary>
public record ScoringCourtDto(int CourtId, string ClubName, string Name);

/// <summary>
/// Upsert payload sent on every persist tick from the live-scoring page.
/// On first call <c>SavedMatchId</c> is null and the server allocates a new
/// row; on subsequent calls it is the previously returned id. <c>DeviceToken</c>
/// is set only when the caller has no signed-in user (anonymous casual
/// scoring). When <c>LinkedFixtureId</c> is provided and the fixture isn't
/// already linked, the server sets fixture <c>SavedMatchId</c> + <c>Status =
/// InProgress</c> as part of the same transaction.
/// </summary>
public record UpsertLiveMatchRequest(
    int? SavedMatchId,
    string MatchJson,
    bool Complete,
    string? Player1, string? Player2, string? Player3, string? Player4,
    int? Player1Id, int? Player2Id, int? Player3Id, int? Player4Id,
    string? WinnerName,
    int? WinnerPlayerId,
    int? CourtId,
    string? DeviceToken,
    int? LinkedFixtureId);

/// <summary>
/// Singles end-of-match cascade payload. Mirrors the inline fixture-update
/// the live-scoring page does today: sets fixture <c>Status = Completed</c>,
/// fills aggregates, links <c>SavedMatchId</c>, runs bracket progression.
/// Distinct from <c>SubmitFixtureScoreRequest</c> on <c>ICompetitionService</c>
/// because the live-scoring path always finalises directly (no
/// AwaitingVerification + email cascade for the singles live-scored result).
/// </summary>
public record FinaliseLiveFixtureRequest(
    int SavedMatchId,
    int? WinnerPlayerId,
    string ResultSummary,
    int HomeSetsWon,
    int AwaySetsWon,
    int HomeGamesTotal,
    int AwayGamesTotal);

// ── Phase 7 PR 7e: IMatchSetupService surface (MatchSetupWizard move) ──

/// <summary>
/// One-shot bootstrap payload for the MatchSetupWizard. Returns the current
/// user's Player profile (PlayerId/DisplayName/AvatarPath via linked
/// account), the user's light-player roster, the user's bookmarked verified
/// players (excluding self), every club for the club autocomplete, and the
/// fuzzy-index seed (light + bookmarked + every other full player so the
/// "Did you mean?" nudge can suggest verified replacements).
/// </summary>
public record MatchSetupBootstrapDto(
    WizardSelfPlayerDto? Me,
    IReadOnlyList<WizardPlayerDto> MyLightPlayers,
    IReadOnlyList<WizardPlayerDto> MyBookmarkedPlayers,
    IReadOnlyList<WizardClubDto> AllClubs,
    IReadOnlyList<WizardFuzzyItemDto> FuzzyIndex);

public record WizardSelfPlayerDto(
    int PlayerId,
    string DisplayName,
    string? AvatarPath);

public record WizardPlayerDto(
    int PlayerId,
    string DisplayName,
    bool IsLight,
    string? AvatarPath);

public record WizardClubDto(int ClubId, string Name);

public record WizardFuzzyItemDto(
    int PlayerId,
    string DisplayName,
    string? FirstName,
    string? LastName,
    bool IsLight);

/// <summary>
/// Court list filtered to a single club, ordered by name. Backs the
/// MatchSetupWizard's club → court drilldown.
/// </summary>
public record WizardCourtDto(int CourtId, string Name);

public record AutoBookmarkPlayersRequest(IReadOnlyList<int> PlayerIds);
