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
    string? EventName);

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
    List<CompetitionParticipantDto> Participants);

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
    string? MobileNumber);

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
    int? WinnerPlayerId);

public record CompetitionTeamDto(
    int CompetitionTeamId,
    int CompetitionId,
    string Name);

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

public record AddStageRequest(string Name, int StageOrder, StageType StageType);

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
