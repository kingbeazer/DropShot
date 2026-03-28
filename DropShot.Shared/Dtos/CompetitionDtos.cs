namespace DropShot.Shared.Dtos;

public record CompetitionDto(
    int CompetitionId,
    string CompetitionName,
    CompetitionFormat CompetitionFormat,
    int? MaxParticipants,
    DateTime? StartDate,
    DateTime? EndDate,
    int? MaxAge,
    PlayerSex? EligibleSex,
    int? HostClubId,
    string? HostClubName,
    int? RulesSetId,
    string? RulesSetName);

public record CompetitionDetailDto(
    int CompetitionId,
    string CompetitionName,
    CompetitionFormat CompetitionFormat,
    int? MaxParticipants,
    DateTime? StartDate,
    DateTime? EndDate,
    int? MaxAge,
    PlayerSex? EligibleSex,
    int? HostClubId,
    string? HostClubName,
    int? RulesSetId,
    string? RulesSetName,
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
    DateTime RegisteredAt);

public record SaveCompetitionRequest(
    string CompetitionName,
    CompetitionFormat CompetitionFormat,
    int? MaxParticipants,
    DateTime? StartDate,
    DateTime? EndDate,
    int? MaxAge,
    PlayerSex? EligibleSex,
    int? HostClubId,
    int? RulesSetId);

public record AddStageRequest(string Name, int StageOrder, StageType StageType);

public record AddParticipantRequest(int PlayerId);

public record UpdateParticipantStatusRequest(ParticipantStatus Status);
