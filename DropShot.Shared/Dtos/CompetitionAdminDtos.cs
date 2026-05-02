namespace DropShot.Shared.Dtos;

// ── Phase 7L–7U: CompetitionPage admin/edit surface ────────────────────────────
//
// DTOs in this file back ICompetitionAdminService — the admin/edit operations on
// the CompetitionPage that don't fit the player-facing ICompetitionService.
// Composes the existing CompetitionDto / CompetitionStageDto / etc. records
// where possible; only types unique to the admin flow live here.

// ── Read DTOs ──────────────────────────────────────────────────────────────────

/// <summary>
/// Fat aggregate returned by <c>ICompetitionAdminService.GetCompetitionForEditAsync</c>.
/// Bundles every list the CompetitionPage needs at initial load so the page makes
/// one round-trip rather than per-tab fetches. Lookup tables (Clubs, RulesSets,
/// Events, Courts, etc.) are included because the create-mode form needs them
/// before any competition exists, and the edit form re-binds them when the host
/// club changes. <c>CompetitionId</c> is null when the page is in create mode.
/// </summary>
public record CompetitionEditDto(
    int? CompetitionId,
    string CompetitionName,
    CompetitionFormat? CompetitionFormat,
    int? MaxParticipants,
    DateTime? StartDate,
    DateTime? EndDate,
    DateTime? RegisterByDate,
    int? MaxAge,
    int? MinAge,
    PlayerSex? EligibleSex,
    int? RulesSetId,
    int? HostClubId,
    string? HostClubName,
    int? EventId,
    string? EventName,
    int BestOf,
    bool RequireVerification,
    bool IsArchived,
    bool IsStarted,
    string? CreatorUserId,
    bool IsRestricted,
    int? TeamSize,
    string? RubberTemplateKey,
    MatchFormatType MatchFormat,
    int NumberOfSets,
    int GamesPerSet,
    SetWinMode SetWinMode,
    LeagueScoringMode LeagueScoring,
    RubberTieBreakMode RubberTieBreak,
    int? MinDaysBetweenPlayerMatches,
    bool HasDivisions,
    int? SeededFromCompetitionId,
    IReadOnlyList<CompetitionStageDto> Stages,
    IReadOnlyList<CompetitionParticipantDto> Participants,
    IReadOnlyList<CompetitionDivisionDto> Divisions,
    IReadOnlyList<CompetitionTeamDto> Teams,
    IReadOnlyList<CompetitionFixtureDto> Fixtures,
    IReadOnlyList<CompetitionMatchWindowDto> MatchWindows,
    IReadOnlyList<CourtPairDto> CourtPairs,
    IReadOnlyList<CompetitionAdminRowDto> Admins,
    RubberTemplateStateDto? RubberTemplate,
    IReadOnlyList<CompetitionSeedSourceDto> SeedSourceCandidates,
    IReadOnlyList<ClubSchedulingTemplateDto> ClubTemplates,
    IReadOnlyList<CompetitionTemplateDto> CompetitionTemplates,
    IReadOnlyList<ClubEmailTemplateDto> EmailTemplates,
    IReadOnlyList<CourtDto> HostClubCourts,
    IReadOnlyList<ClubDto> Clubs,
    IReadOnlyList<RulesSetDto> RulesSets,
    IReadOnlyList<EventDto> Events,
    IReadOnlyList<int> AllowedPlayerIds,
    bool CanEdit,
    bool IsSuperAdmin);

public record CompetitionMatchWindowDto(
    int CompetitionMatchWindowId,
    int CompetitionId,
    int? CourtId,
    string? CourtName,
    int? CompetitionDivisionId,
    string? DivisionName,
    DayOfWeek DayOfWeek,
    TimeSpan StartTime,
    TimeSpan EndTime);

public record CompetitionTemplateDto(
    int CompetitionTemplateId,
    int ClubId,
    string Name,
    CompetitionFormat? Format,
    int? RulesSetId,
    int? BestOf,
    int? MaxAge,
    PlayerSex? EligibleSex,
    IReadOnlyList<CompetitionTemplateWindowDto> Windows);

public record CompetitionTemplateWindowDto(
    int CompetitionTemplateWindowId,
    DayOfWeek DayOfWeek,
    TimeSpan StartTime,
    TimeSpan EndTime);

public record CompetitionAdminRowDto(
    string UserId,
    string UserEmail,
    DateTime AssignedAt);

/// <summary>
/// Per-division match-window form state. Replaces the nested
/// <c>CompetitionPage.DivisionWindowForm</c> type so DivisionsSection (post-RCL
/// move) can bind to a Shared type instead of a parent-page nested class.
/// </summary>
public record DivisionWindowFormDto
{
    public DayOfWeek Day { get; init; } = DayOfWeek.Monday;
    public int? CourtId { get; init; }
    public TimeSpan? Start { get; init; }
    public TimeSpan? End { get; init; }
    public int? EditingId { get; init; }
}

public record ClubSchedulingTemplateDto(
    int ClubSchedulingTemplateId,
    int ClubId,
    string Name,
    IReadOnlyList<ClubSchedulingTemplateWindowDto> Windows);

public record ClubSchedulingTemplateWindowDto(
    int ClubSchedulingTemplateWindowId,
    DayOfWeek DayOfWeek,
    TimeSpan StartTime,
    TimeSpan EndTime);

/// <summary>
/// A competition that could seed divisions for a new competition (same format,
/// same host club, finished). Surfaced in the seed-divisions dialog.
/// </summary>
public record CompetitionSeedSourceDto(
    int CompetitionId,
    string CompetitionName,
    DateTime? EndDate,
    int DivisionCount);

public record ClubEmailTemplateDto(
    int ClubEmailTemplateId,
    int ClubId,
    string Name,
    string Subject,
    string Body);

/// <summary>
/// Resolved rubber-template state for a competition, returned by
/// <c>LoadRubberTemplateStateAsync</c>. <c>Source</c> is "default" / "preset" /
/// "custom" — drives which controls render in RubberTemplateSection.
/// </summary>
public record RubberTemplateStateDto(
    string Source,
    string? SelectedPresetKey,
    IReadOnlyList<RubberDef> Defs,
    IReadOnlyList<RubberPresetDto> AvailablePresets,
    IReadOnlyList<string> AvailableRoles);

public record GeneratedTeamPreviewDto(
    string Name,
    IReadOnlyList<int> MemberPlayerIds,
    IReadOnlyDictionary<int, string> Roles,
    int? DivisionId,
    string? DivisionName);

// ── Request DTOs ──────────────────────────────────────────────────────────────

/// <summary>
/// Save (create + update) for the full competition admin form. Wider than the
/// player-facing <c>SaveCompetitionRequest</c>: includes all match-config /
/// rubber-template / scheduling knobs plus the admin-only restricted-allow-list.
/// </summary>
public record SaveCompetitionEditRequest(
    string CompetitionName,
    CompetitionFormat CompetitionFormat,
    PlayerSex? EligibleSex,
    int? MaxParticipants,
    DateTime? StartDate,
    DateTime? EndDate,
    DateTime? RegisterByDate,
    int? MaxAge,
    int? MinAge,
    int? HostClubId,
    int? RulesSetId,
    int? EventId,
    int BestOf,
    bool RequireVerification,
    int? TeamSize,
    string? RubberTemplateKey,
    MatchFormatType MatchFormat,
    int NumberOfSets,
    int GamesPerSet,
    SetWinMode SetWinMode,
    LeagueScoringMode LeagueScoring,
    RubberTieBreakMode RubberTieBreak,
    int? MinDaysBetweenPlayerMatches,
    bool HasDivisions,
    int? SeededFromCompetitionId,
    bool IsRestricted = false,
    IReadOnlyList<int>? AllowedPlayerIds = null);

public record ApplyStageFollowUpRequest(IReadOnlyList<StageType> StageTypes);

public record SaveMatchWindowRequest(
    DayOfWeek DayOfWeek,
    TimeSpan StartTime,
    TimeSpan EndTime,
    int? CourtId,
    int? CompetitionDivisionId);

public record ImportMatchWindowsFromTemplateRequest(int ClubSchedulingTemplateId);

public record GenerateTeamsRequest(
    int? TeamSize,
    bool BalanceByGender,
    int? CompetitionDivisionId);

public record GenerateTeamsResultDto(
    IReadOnlyList<GeneratedTeamPreviewDto> Preview,
    IReadOnlyList<string> Warnings);

public record ConfirmGenerateTeamsRequest(
    IReadOnlyList<GeneratedTeamPreviewDto> Teams);

public record ScheduleFixturesAdminRequest(
    bool DeleteExistingUnscheduled);

public record ScheduleFixturesResultDto(
    int FixturesScheduled,
    int FixturesSkipped,
    IReadOnlyList<string> Warnings);

public record CloneCompetitionRequest(
    string NewName,
    bool CopyParticipants);

public record CloneCompetitionResultDto(int NewCompetitionId);

public record ApplyRubberPresetRequest(string? PresetKey);

public record SaveRubberRowRequest(
    int? RowId,
    int Order,
    string Name,
    int CourtNumber,
    IReadOnlyList<string> HomeRoles,
    IReadOnlyList<string> AwayRoles);

public record AddCompetitionAdminRequest(string Email);

public record SimulateRoundRobinResultDto(int FixturesUpdated);

public record SeedKnockoutFromStandingsResultDto(
    int FixturesSeeded,
    IReadOnlyList<string> Warnings);

public record SendMatchEmailRequest(
    int FixtureId,
    string Subject,
    string Body);

public record SendCompetitionEmailRequest(
    string Subject,
    string Body);

public record CreateLightPlayerForCompetitionRequest(
    string DisplayName,
    string? FirstName,
    string? LastName,
    string? Email,
    string? MobileNumber,
    PlayerSex? Sex,
    DateTime? DateOfBirth,
    int? HostClubId);

public record SaveLightPlayerRequest(
    string DisplayName,
    string? FirstName,
    string? LastName,
    string? Email,
    string? MobileNumber,
    PlayerSex? Sex,
    DateTime? DateOfBirth);

public record SearchPlayersForAddRequest(
    string Query,
    int? HostClubId,
    int? MaxResults = 20);
