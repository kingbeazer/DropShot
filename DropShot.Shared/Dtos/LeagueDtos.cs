namespace DropShot.Shared.Dtos;

public enum LeagueSeasonStatusDto : byte
{
    Planning = 1,
    Active = 2,
    Closed = 3,
}

public record LeagueSummaryDto(
    int LeagueId,
    string Name,
    int HostClubId,
    string? HostClubName,
    bool IsArchived,
    int SeasonCount,
    int MembershipCount);

public record LeagueDetailDto(
    int LeagueId,
    string Name,
    int HostClubId,
    string? HostClubName,
    bool IsArchived,
    CompetitionFormat CompetitionFormat,
    int TeamSize,
    LeagueScoringMode LeagueScoring,
    string? RubberTemplateKey,
    MatchFormatType MatchFormat,
    int NumberOfSets,
    int GamesPerSet,
    SetWinMode SetWinMode,
    int TeamsPerDivisionTarget,
    int TeamsPerDivisionMin);

public record CreateLeagueRequest(
    string Name,
    int HostClubId,
    CompetitionFormat CompetitionFormat = CompetitionFormat.TeamMatch,
    int TeamSize = 4,
    LeagueScoringMode LeagueScoring = LeagueScoringMode.WinPoints,
    string? RubberTemplateKey = null,
    MatchFormatType MatchFormat = MatchFormatType.BestOf,
    int NumberOfSets = 3,
    int GamesPerSet = 6,
    SetWinMode SetWinMode = SetWinMode.WinBy2,
    int TeamsPerDivisionTarget = 8,
    int TeamsPerDivisionMin = 6);

public record UpdateLeagueRequest(
    string Name,
    CompetitionFormat CompetitionFormat,
    int TeamSize,
    LeagueScoringMode LeagueScoring,
    string? RubberTemplateKey,
    MatchFormatType MatchFormat,
    int NumberOfSets,
    int GamesPerSet,
    SetWinMode SetWinMode,
    int TeamsPerDivisionTarget,
    int TeamsPerDivisionMin,
    bool IsArchived = false);

public record LeagueMembershipDto(
    int PlayerId,
    string DisplayName,
    DateTime JoinedAt,
    bool IsActive,
    byte? CurrentDivisionRank);

public record EnrolPlayerRequest(int PlayerId);

public record LeagueSeasonSummaryDto(
    int LeagueSeasonId,
    int LeagueId,
    string Name,
    DateTime? StartDate,
    DateTime? EndDate,
    LeagueSeasonStatusDto Status,
    DateTime? ClosedAt,
    int DivisionCount);

public record CreateSeasonRequest(
    string Name,
    DateTime? StartDate = null,
    DateTime? EndDate = null);

public record DivisionBucketDto(
    byte Rank,
    string Name,
    IReadOnlyList<int> PlayerIds);

public record DivisionSuggestionDto(
    IReadOnlyList<DivisionBucketDto> Buckets,
    IReadOnlyList<int> UnassignedPlayerIds);

public record DivisionTeamAssignmentDto(
    string TeamName,
    IReadOnlyList<PlayerRoleAssignmentDto> Members);

public record PlayerRoleAssignmentDto(int PlayerId, string? Role);

public record MaterialiseDivisionDto(
    byte Rank,
    string Name,
    IReadOnlyList<DivisionTeamAssignmentDto> Teams);

public record CreateDivisionsRequest(
    IReadOnlyList<MaterialiseDivisionDto> Divisions);

public record LeagueDivisionDto(
    int LeagueDivisionId,
    byte Rank,
    string Name,
    int CompetitionId,
    string CompetitionName,
    int TeamCount,
    int PlayerCount);

public record LeagueSeasonDetailDto(
    int LeagueSeasonId,
    int LeagueId,
    string Name,
    DateTime? StartDate,
    DateTime? EndDate,
    LeagueSeasonStatusDto Status,
    DateTime? ClosedAt,
    IReadOnlyList<LeagueDivisionDto> Divisions);

public record PlayerStatsDto(
    int PlayerId,
    string DisplayName,
    int DivisionRank,
    string DivisionName,
    int RubbersPlayed,
    int RubbersWon,
    int RubbersLost,
    int SetsWon,
    int SetsAgainst,
    int GamesWon,
    int GamesAgainst,
    int LeaguePoints,
    double WinRate);

public record PromotionCandidateDto(
    int PlayerId,
    string DisplayName,
    byte CurrentRank,
    int PositionInDivision,
    int RubbersPlayed,
    int LeaguePoints,
    double WinRate);

public record PromotionPreviewDto(
    int LeagueSeasonId,
    IReadOnlyList<PromotionDivisionDto> Divisions);

public record PromotionDivisionDto(
    byte Rank,
    string Name,
    IReadOnlyList<PromotionCandidateDto> Players);

public record PromotionDecisionDto(int PlayerId, byte NewRank);

public record CloseSeasonRequest(IReadOnlyList<PromotionDecisionDto> Decisions);

public record CloseSeasonResultDto(int SeasonId, int DecisionsApplied);
