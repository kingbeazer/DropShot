namespace DropShot.Shared.Dtos;

public record CompetitionDto(
    int CompetitionId,
    string CompetitionName,
    CompetitionFormat CompetitionFormat,
    int? MaxParticipants,
    DateTime? StartDate,
    DateTime? EndDate,
    int? MaxAge,
    int? MinAge,
    PlayerSex? EligibleSex,
    int? HostClubId,
    string? HostClubName,
    int? RulesSetId,
    string? RulesSetName,
    int? EventId,
    string? EventName,
    bool IsArchived = false,
    bool IsStarted = false,
    string? CreatorUserId = null,
    bool IsRestricted = false,
    DateTime? RegisterByDate = null);

public record CompetitionDetailDto(
    int CompetitionId,
    string CompetitionName,
    CompetitionFormat CompetitionFormat,
    int? MaxParticipants,
    DateTime? StartDate,
    DateTime? EndDate,
    int? MaxAge,
    int? MinAge,
    PlayerSex? EligibleSex,
    int? HostClubId,
    string? HostClubName,
    int? RulesSetId,
    string? RulesSetName,
    int? EventId,
    string? EventName,
    List<CompetitionStageDto> Stages,
    List<CompetitionParticipantDto> Participants,
    bool IsArchived = false,
    bool IsStarted = false,
    string? CreatorUserId = null,
    bool IsRestricted = false,
    List<int>? AllowedPlayerIds = null,
    bool HasDivisions = false,
    List<CompetitionDivisionDto>? Divisions = null,
    List<CompetitionFixtureDto>? Fixtures = null,
    List<CompetitionTeamDto>? Teams = null,
    List<CourtPairDto>? CourtPairs = null,
    LeagueScoringMode LeagueScoring = LeagueScoringMode.WinPoints,
    int? MyPlayerId = null);

public record CompetitionStageDto(
    int CompetitionStageId,
    string Name,
    int StageOrder,
    StageType StageType);

public record CompetitionParticipantDto(
    int PlayerId,
    string DisplayName,
    ParticipantStatus Status,
    DateTime RegisteredAt,
    int? TeamId,
    string? TeamName,
    string? MobileNumber,
    string? Role = null,
    PlayerSex? Sex = null,
    int? CompetitionDivisionId = null,
    string? DivisionName = null);

public record CompetitionFixtureDto(
    int CompetitionFixtureId,
    int CompetitionId,
    int? CompetitionStageId,
    string? StageName,
    int? CourtId,
    string? CourtName,
    DateTime? ScheduledAt,
    FixtureStatus Status,
    string? FixtureLabel,
    int? RoundNumber,
    int? Player1Id,
    string? Player1Name,
    int? Player2Id,
    string? Player2Name,
    int? Player3Id,
    string? Player3Name,
    int? Player4Id,
    string? Player4Name,
    string? ResultSummary,
    int? WinnerPlayerId,
    int? HomeTeamId = null,
    string? HomeTeamName = null,
    int? AwayTeamId = null,
    string? AwayTeamName = null,
    int? WinnerTeamId = null,
    int? CourtPairId = null,
    string? CourtPairName = null,
    IReadOnlyList<RubberDto>? Rubbers = null,
    DateTime? CompletedAt = null,
    string? OriginalResultSummary = null,
    bool ResultModifiedByAdmin = false,
    string? CompetitionName = null);

public record CompetitionTeamDto(
    int CompetitionTeamId,
    int CompetitionId,
    string Name,
    int? CaptainPlayerId = null,
    string? CaptainName = null,
    int? CompetitionDivisionId = null);

public record LeagueTableEntryDto(
    int PlayerId,
    string PlayerName,
    int Played,
    int Won,
    int Lost,
    int Points);

/// <summary>
/// Bundled view for the "/competitions" page from a non-admin user's
/// perspective: the competitions they've already entered + the ones currently
/// open to them. <c>HasPlayer</c> is false when the authenticated user has no
/// linked Player row, in which case the page shows the "create a profile"
/// hint and the two lists are empty.
/// </summary>
public record MyCompetitionsViewDto(
    bool HasPlayer,
    List<CompetitionDto> Entered,
    List<CompetitionDto> Available);

public record SaveCompetitionRequest(
    string CompetitionName,
    CompetitionFormat CompetitionFormat,
    int? MaxParticipants,
    DateTime? StartDate,
    DateTime? EndDate,
    int? MaxAge,
    int? MinAge,
    PlayerSex? EligibleSex,
    int? HostClubId,
    int? RulesSetId,
    int? EventId,
    bool IsRestricted = false,
    List<int>? AllowedPlayerIds = null,
    bool HasDivisions = false,
    int? SeededFromCompetitionId = null);

public record AddStageRequest(StageType StageType, string? Name = null, int? StageOrder = null);

public record AddParticipantRequest(int PlayerId, bool Force = false);

/// <summary>
/// Approve an awaiting-verification fixture result, optionally overriding the
/// score. When <c>OverrideScores</c> is non-null, the original ResultSummary
/// + WinnerPlayerId are preserved on the fixture for audit and the new score
/// becomes authoritative.
/// </summary>
public record ApproveFixtureResultRequest(
    FixtureScoreOverride? OverrideScores = null);

public record FixtureScoreOverride(
    string ResultSummary,
    int WinnerPlayerId,
    int HomeSetsWon,
    int AwaySetsWon,
    int HomeGamesTotal,
    int AwayGamesTotal);

/// <summary>
/// Submit a fixture score for the first time. Used by SubmitScoreDialog.
/// The dialog validates set scores client-side; the request carries the
/// already-validated, summarised result. <c>WinnerPlayerId</c> may be null
/// in fixed-set mode when the match was tied. <c>AdminOverride</c> bypasses
/// "RequireVerification" — admin submissions go straight to Completed and
/// preserve any prior result for audit.
/// </summary>
public record SubmitFixtureScoreRequest(
    string ResultSummary,
    int? WinnerPlayerId,
    int HomeSetsWon,
    int AwaySetsWon,
    int HomeGamesTotal,
    int AwayGamesTotal,
    bool AdminOverride);

/// <summary>
/// Response body returned with HTTP 409 when an admin action would violate a
/// competition's eligibility rules (sex / age / allow-list / mixed-doubles
/// pairing / MTT composition). The admin UI shows these as a confirmation
/// prompt; re-posting the request with <c>Force = true</c> overrides the guard.
/// </summary>
public record EligibilityWarning(string Code, string Message);

public record EligibilityWarningsResponse(string Message, List<EligibilityWarning> Warnings);

public record UpdateParticipantStatusRequest(ParticipantStatus Status);

public record SaveFixtureRequest(
    int? CompetitionStageId,
    int? CourtId,
    DateTime? ScheduledAt,
    string? FixtureLabel,
    int? RoundNumber,
    int? Player1Id,
    int? Player2Id,
    int? Player3Id,
    int? Player4Id,
    FixtureStatus Status,
    string? ResultSummary = null,
    int? WinnerPlayerId = null,
    bool Force = false);

public record SaveTeamRequest(string Name);

public record AssignParticipantTeamRequest(int? TeamId);

// ── Event DTOs ──────────────────────────────────────────────────────────────

public record EventDto(
    int EventId,
    string Name,
    string? Description,
    DateTime? StartDate,
    DateTime? EndDate,
    int? HostClubId,
    string? HostClubName,
    int CompetitionCount);

public record EventDetailDto(
    int EventId,
    string Name,
    string? Description,
    DateTime? StartDate,
    DateTime? EndDate,
    int? HostClubId,
    string? HostClubName,
    List<CompetitionDto> Competitions);

public record SaveEventRequest(
    string Name,
    string? Description,
    DateTime? StartDate,
    DateTime? EndDate,
    int? HostClubId);

public record CreateEventCompetitionsRequest(
    List<EventCompetitionTemplate> Competitions);

public record EventCompetitionTemplate(
    string CompetitionName,
    CompetitionFormat CompetitionFormat,
    PlayerSex? EligibleSex,
    int? MaxAge,
    int? MinAge);

// ── Team Match DTOs ──────────────────────────────────────────────────────────

public record CourtPairDto(
    int CourtPairId,
    int CompetitionId,
    int Court1Id,
    string Court1Name,
    int Court2Id,
    string Court2Name,
    string Name);

public record RubberDto(
    int RubberId,
    int CompetitionFixtureId,
    int Order,
    string Name,
    int CourtNumber,
    IReadOnlyList<string> HomeRoles,
    IReadOnlyList<string> AwayRoles,
    int? HomePlayer1Id,
    string? HomePlayer1Name,
    int? HomePlayer2Id,
    string? HomePlayer2Name,
    int? AwayPlayer1Id,
    string? AwayPlayer1Name,
    int? AwayPlayer2Id,
    string? AwayPlayer2Name,
    int? HomeGames,
    int? AwayGames,
    int? WinnerTeamId,
    bool IsComplete,
    int? SavedMatchId,
    int? HomeSetsWon = null,
    int? AwaySetsWon = null,
    int? HomeGamesTotal = null,
    int? AwayGamesTotal = null);

public record TeamLeagueTableEntryDto(
    int TeamId,
    string TeamName,
    string? CaptainName,
    int Played,
    int Won,
    int Drawn,
    int Lost,
    int RubbersWon,
    int RubbersAgainst,
    int Points,
    int ScoringFor = 0,
    int ScoringAgainst = 0,
    string ScoringUnitLabel = "rubbers");

public record SaveCourtPairRequest(int Court1Id, int Court2Id, string Name);

public record SetParticipantRoleRequest(string? Role);

// ── Divisions (multi-tier within a competition) ──────────────────────────────

public record CompetitionDivisionDto(
    int CompetitionDivisionId,
    int CompetitionId,
    byte Rank,
    string Name);

public record SaveDivisionRequest(string Name, byte Rank);

public record SetParticipantDivisionRequest(int? CompetitionDivisionId);

public record SeedDivisionsFromPreviousRequest(
    int PreviousCompetitionId,
    bool ApplyPromotion = false,
    int PromoteCount = 0,
    int DemoteCount = 0);

public record SeedDivisionsResultDto(
    int DivisionsCreated,
    int ParticipantsAssigned);

public record RubberTemplateDefDto(
    int Order,
    string Name,
    int CourtNumber,
    IReadOnlyList<string> HomeRoles,
    IReadOnlyList<string> AwayRoles);

public record RubberTemplateDto(
    string Source,                                // "custom" | "preset:<key>" | "default"
    string? PresetKey,
    IReadOnlyList<string> AvailableRoles,
    IReadOnlyList<RubberTemplateDefDto> Rubbers);

public record RubberPresetDto(string Key, string Label);

public record SaveRubberTemplateRequest(
    IReadOnlyList<RubberTemplateDefDto> Rubbers);

public record SetCompetitionRubberTemplateKeyRequest(string? TemplateKey);

public record SetTeamCaptainRequest(int CaptainPlayerId);

public record TeamValidationResultDto(bool IsValid, List<string> Errors);

/// <summary>
/// Bundle returned to the rubber-scoring dialogs (single + bulk) and the
/// TeamMatchScoring page. Carries the fixture's match-config knobs so the
/// dialog can render set inputs and run per-set validation client-side
/// without a separate competition fetch. <c>IsAlreadyFinalised</c> tells
/// admin-edit flows whether the fixture is AwaitingVerification/Completed
/// (in which case re-submitting an admin score updates aggregates in place
/// rather than regenerating verification tokens). <c>LeagueScoring</c> and
/// <c>HostClubId</c> support the TeamMatchScoring page's running-score chip
/// and admin-authorisation gate.
/// </summary>
public record FixtureRubberContextDto(
    int CompetitionFixtureId,
    int CompetitionId,
    string? CompetitionName,
    string? FixtureLabel,
    int? HomeTeamId,
    int? AwayTeamId,
    string HomeTeamName,
    string AwayTeamName,
    MatchFormatType MatchFormat,
    int BestOf,
    int NumberOfSets,
    int GamesPerSet,
    SetWinMode SetWinMode,
    bool RequireVerification,
    bool IsAlreadyFinalised,
    IReadOnlyList<RubberDialogDto> Rubbers,
    LeagueScoringMode LeagueScoring = LeagueScoringMode.WinPoints,
    int? HostClubId = null);

public record RubberDialogDto(
    int RubberId,
    int Order,
    string Name,
    int CourtNumber,
    int? HomePlayer1Id,
    string? HomePlayer1Name,
    int? HomePlayer2Id,
    string? HomePlayer2Name,
    int? AwayPlayer1Id,
    string? AwayPlayer1Name,
    int? AwayPlayer2Id,
    string? AwayPlayer2Name,
    bool IsComplete,
    int? SavedMatchId,
    IReadOnlyList<RubberSetScoreDto> ExistingSetScores,
    int? WinnerTeamId = null,
    int? HomeGames = null,
    int? AwayGames = null,
    int? HomeSetsWon = null,
    int? AwaySetsWon = null,
    int? HomeGamesTotal = null,
    int? AwayGamesTotal = null);

public record RubberSetScoreDto(int Home, int Away);

/// <summary>
/// Bulk-submit rubber scores for a fixture. The single-rubber dialog sends
/// one entry; the "enter all scores" dialog sends every rubber. Server runs
/// the same fixture-finalisation cascade either way: persists the rubber
/// rows, then if every rubber is complete runs the score / tie-break
/// resolution + notification or verification email pipeline + bracket
/// progression. <c>AdminOverride = true</c> bypasses RequireVerification
/// and updates aggregates in place when the fixture is already finalised
/// (no verification-token regeneration, no resent emails).
/// </summary>
public record SubmitRubberScoresRequest(
    bool AdminOverride,
    IReadOnlyList<RubberScoreEntry> Scores);

// ── Phase 7 PR 7j: VerifyResult.razor surface ──

/// <summary>
/// One-shot view payload for the <c>/verify-result/{token}</c> page.
/// Server resolves the fixture by VerificationToken (only matches when
/// <c>Status == AwaitingVerification</c>); per-set scores for team rubbers
/// and the side aggregates for league-table use are pre-computed so the
/// RCL doesn't have to run the resolution logic itself.
/// </summary>
public record VerifyFixtureViewDto(
    int CompetitionFixtureId,
    int CompetitionId,
    string? CompetitionName,
    string? FixtureLabel,
    bool IsTeamMatch,
    string Side1,
    string Side2,
    int? HomeTeamId,
    int? AwayTeamId,
    int? Player1Id,
    int? Player2Id,
    int? WinnerPlayerId,
    int? WinnerTeamId,
    string? ResultSummary,
    int BestOf,
    int AggregateHome,
    int AggregateAway,
    string AggregateUnit,
    string SecondaryAggregate,
    bool AllRubbersComplete,
    string RubberTieBreak,
    IReadOnlyList<VerifyRubberDto> Rubbers);

public record VerifyRubberDto(
    int RubberId,
    int Order,
    string Name,
    string? HomePair,
    string? AwayPair,
    int? HomeSetsWon,
    int? AwaySetsWon,
    bool IsComplete,
    int? WinnerTeamId,
    IReadOnlyList<RubberSetScoreDto> SetScores);

public record ApproveFixtureByTokenRequest(
    FixtureScoreOverride? OverrideScores,
    int? ManualWinnerTeamId);

public record ApproveFixtureByTokenResultDto(
    bool Success,
    string? ErrorMessage,
    int? CompetitionId,
    bool WasModified);

public record RubberScoreEntry(
    int RubberId,
    int HomeSetsWon,
    int AwaySetsWon,
    int HomeGamesTotal,
    int AwayGamesTotal,
    int? LastSetHomeGames,
    int? LastSetAwayGames,
    IReadOnlyList<RubberSetScoreDto> SetScores);
