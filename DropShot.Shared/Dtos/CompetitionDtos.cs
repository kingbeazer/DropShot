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
    int? MyPlayerId = null,
    string? Description = null,
    double LadderKFactor = 20.0,
    double LadderStartingRating = 1000.0,
    int LadderProvisionalMatches = 10,
    bool LadderUseMarginOfVictory = true,
    List<LadderInactivityDecayDto>? LadderDecayEvents = null,
    // The current user's own MobileNumber (or null if they have no Player
    // record / no number on file). Used by the entry consent dialog to
    // render the masked number and decide whether to block entry until a
    // number is added. Always populated for the caller themselves regardless
    // of peer visibility — viewing your own number doesn't require consent.
    string? MyMobileNumber = null);

public record LadderInactivityDecayDto(
    int PlayerId,
    string PlayerName,
    DateTime AppliedAt,
    double RatingBefore,
    double RatingAfter,
    int DaysInactive);

/// <summary>
/// Result of the SuperAdmin "simulate N weeks" tool — counts of synthetic
/// fixtures and decay events produced.
/// </summary>
public record LadderSimulationResultDto(
    int Participants,
    int ActivePlayers,
    int IdlePlayers,
    int FixturesGenerated,
    int DecayEventsGenerated);

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
    string? DivisionName = null,
    PlayerRatingDto? Rating = null,
    PlayerRatingSuggestionDto? RatingSuggestion = null,
    PlacementSuggestionDto? PlacementSuggestion = null,
    double LadderEloRating = 1000.0,
    int LadderMatchesPlayed = 0,
    bool LadderIsProvisional = true,
    DateTime? LadderLastMatchAt = null);

public record PlayerRatingDto(double CurrentRating, bool IsProvisional);

public record PlayerRatingSuggestionDto(
    double PreviousRating,
    double SuggestedRating,
    double Delta,
    int RubbersPlayed);

public record PlacementSuggestionDto(
    int? SuggestedDivisionId,
    string? SuggestedDivisionName,
    string? SuggestedRole);

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
    string? CompetitionName = null,
    double? Player1RatingBefore = null,
    double? Player1RatingAfter = null,
    double? Player2RatingBefore = null,
    double? Player2RatingAfter = null,
    int? HomeGamesTotal = null,
    int? AwayGamesTotal = null);

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
    List<CompetitionDto> Available,
    // Caller's own MobileNumber (null when no Player record / no number on
    // file). Used by the per-competition entry consent dialog to render the
    // masked number and to gate the Enter button until a number is added.
    string? MyMobileNumber = null);

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
    int? SeededFromCompetitionId = null,
    double LadderKFactor = 20.0,
    double LadderStartingRating = 1000.0,
    int LadderProvisionalMatches = 10,
    bool LadderUseMarginOfVictory = true);

public record AddStageRequest(StageType StageType, string? Name = null, int? StageOrder = null);

/// <summary>
/// Admin attestation that the data subject (the player being added) has
/// consented to their mobile number being shared with other competitors —
/// typically because they emailed the admin asking to enter. Materially
/// weaker than self-asserted consent (the player didn't click anything
/// themselves), so the recorded ConsentVersion is distinct ("v1-2026-05-admin")
/// and <see cref="Source"/> captures the admin's evidence (subject line,
/// date, channel) for audit. Players retain self-service withdrawal via
/// Leave competition.
/// </summary>
public record AdminRecordedPhoneShareConsent(
    bool Attested,
    string Source);

public record AddParticipantRequest(
    int PlayerId,
    bool Force = false,
    // Null = no peer-share consent recorded (e.g. test data, simulated
    // rosters). Server will not throw — the visibility service simply
    // keeps the number hidden from peers until the player self-consents.
    AdminRecordedPhoneShareConsent? AttestedConsent = null);

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
/// Submit a fixture score for the first time. Used by the SubmitScorePage
/// (/match/submit/{fixtureId}). The page validates set scores client-side;
/// the request carries the already-validated, summarised result.
/// <c>WinnerPlayerId</c> may be null in fixed-set mode when the match was
/// tied. <c>AdminOverride</c> bypasses "RequireVerification" — admin
/// submissions go straight to Completed and preserve any prior result for
/// audit.
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
/// Bundled payload the SubmitScorePage loads to render itself: the fixture
/// it's scoring plus the competition's match-config knobs (so the chip rows,
/// set count, and validation match the competition's actual rules rather
/// than falling back to defaults). <c>CanAdminOverride</c> is true when the
/// authenticated caller is a competition admin — the page uses it to honour
/// (or quietly ignore) the <c>?admin=1</c> query flag.
/// </summary>
public record FixtureScoreContextDto(
    CompetitionFixtureDto Fixture,
    MatchFormatType MatchFormat,
    int NumberOfSets,
    int BestOf,
    int GamesPerSet,
    SetWinMode SetWinMode,
    bool CanAdminOverride,
    int FinalSetTieBreakGames = 10,
    SetWinMode FinalSetTieBreakWinMode = SetWinMode.WinBy2);

/// <summary>
/// Response body returned with HTTP 409 when an admin action would violate a
/// competition's eligibility rules (sex / age / allow-list / mixed-doubles
/// pairing / MTT composition). The admin UI shows these as a confirmation
/// prompt; re-posting the request with <c>Force = true</c> overrides the guard.
/// </summary>
public record EligibilityWarning(string Code, string Message);

public record EligibilityWarningsResponse(string Message, List<EligibilityWarning> Warnings);

public record UpdateParticipantStatusRequest(ParticipantStatus Status);

/// <summary>
/// Per-competition consent payload submitted when a player enters a competition.
/// <c>WordingShown</c> is the exact text the user saw (so it can be recorded
/// verbatim for audit) and <c>Version</c> matches the server's
/// <c>PhoneVisibilityService.CurrentConsentVersion</c> — mismatches are
/// rejected so a stale client reloads.
/// </summary>
public record PhoneShareConsent(
    bool Granted,
    string WordingShown,
    string Version);

/// <summary>
/// Self-register / confirm-participation request body. Carries the chosen
/// participation status plus the per-competition phone-share consent the user
/// gave in the dialog.
/// </summary>
public record SelfRegisterRequest(
    ParticipantStatus Status,
    PhoneShareConsent Consent);

/// <summary>
/// Enter-competition request body. Carries the per-competition phone-share
/// consent and the participation status the user chose (FullPlayer or
/// Substitute). The server rejects any other status value.
/// </summary>
public record EnterCompetitionRequest(
    PhoneShareConsent Consent,
    ParticipantStatus Status = ParticipantStatus.FullPlayer);

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
    bool Force = false,
    int? HomeTeamId = null,
    int? AwayTeamId = null,
    int? CourtPairId = null);

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
    int? AwayGamesTotal = null,
    IReadOnlyList<RubberSetScoreDto>? SetScores = null);

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

public record SetParticipantInitialRatingRequest(double Rating);

public record ApplyDivisionPlacementRequest(int CompetitionDivisionId);

public record ApplyRolePlacementRequest(string Role);

// ── Divisions (multi-tier within a competition) ──────────────────────────────

public record CompetitionDivisionDto(
    int CompetitionDivisionId,
    int CompetitionId,
    byte Rank,
    string Name,
    bool UseSharedMatchWindows = true);

public record SaveDivisionRequest(string Name, byte Rank, bool UseSharedMatchWindows = true);

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
    int? HostClubId = null,
    // True when the calling user is allowed to score this fixture — i.e.
    // they're a competition admin OR a participant on one of the two
    // teams. Pages that exist solely for score entry redirect home when
    // this is false so non-participants can't open them by URL.
    bool CanUserScore = false);

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
