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
    bool IsArchived = false);

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
    bool IsArchived = false);

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
    PlayerGrade? Grade = null,
    PlayerSex? Sex = null);

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
    string? CourtPairName = null);

public record CompetitionTeamDto(
    int CompetitionTeamId,
    int CompetitionId,
    string Name,
    int? CaptainPlayerId = null,
    string? CaptainName = null);

public record LeagueTableEntryDto(
    int PlayerId,
    string PlayerName,
    int Played,
    int Won,
    int Lost,
    int Points);

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
    int? EventId);

public record AddStageRequest(StageType StageType, string? Name = null, int? StageOrder = null);

public record AddParticipantRequest(int PlayerId);

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
    int? WinnerPlayerId = null);

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

// ── Mixed Team Tennis DTOs ──────────────────────────────────────────────────

public record CourtPairDto(
    int CourtPairId,
    int CompetitionId,
    int Court1Id,
    string Court1Name,
    int Court2Id,
    string Court2Name,
    string Name);

public record TeamMatchSetDto(
    int TeamMatchSetId,
    int CompetitionFixtureId,
    int SetNumber,
    TeamMatchPhase Phase,
    TeamMatchSetType SetType,
    int CourtNumber,
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
    int? SavedMatchId);

public record TeamLeagueTableEntryDto(
    int TeamId,
    string TeamName,
    string? CaptainName,
    int Played,
    int Won,
    int Drawn,
    int Lost,
    int SetsWon,
    int SetsAgainst,
    int Points);

public record SaveCourtPairRequest(int Court1Id, int Court2Id, string Name);

public record SetParticipantGradeRequest(PlayerGrade Grade);

public record SetTeamCaptainRequest(int CaptainPlayerId);

public record TeamValidationResultDto(bool IsValid, List<string> Errors);
