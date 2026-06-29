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
    int FinalSetTieBreakGames,
    SetWinMode FinalSetTieBreakWinMode,
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
    IReadOnlyList<CompetitionCalendarExceptionDto> CalendarExceptions,
    bool CanEdit,
    bool IsSuperAdmin,
    string? Description = null,
    int? WizardStep = null,
    IReadOnlyList<CompetitionFixtureReminderDto>? FixtureReminders = null);

public record CompetitionCalendarExceptionDto(
    int CompetitionCalendarExceptionId,
    int CompetitionId,
    int? CompetitionDivisionId,
    string? DivisionName,
    DateOnly ExceptionDate,
    string? Note);

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
public class DivisionWindowFormDto
{
    public DayOfWeek Day { get; set; } = DayOfWeek.Monday;
    public int? CourtId { get; set; }
    public TimeSpan? Start { get; set; }
    public TimeSpan? End { get; set; }
    public int? EditingId { get; set; }
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
    int FinalSetTieBreakGames = 10,
    SetWinMode FinalSetTieBreakWinMode = SetWinMode.WinBy2,
    bool IsRestricted = false,
    IReadOnlyList<int>? AllowedPlayerIds = null,
    string? Description = null,
    double LadderKFactor = 20.0,
    double LadderStartingRating = 1000.0,
    int LadderProvisionalMatches = 10,
    bool LadderUseMarginOfVictory = true,
    int? WizardStep = null);

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

/// <summary>
/// Bulk-add multiple players to a competition in one round-trip. Each player is
/// enrolled with <see cref="Status"/>; players already in the competition or
/// who don't exist are silently skipped (counted via the result DTO).
/// Eligibility violations are bypassed because the calling UI shows only
/// eligible candidates in the picker.
/// </summary>
public record BulkAddParticipantsRequest(
    IReadOnlyList<int> PlayerIds,
    ParticipantStatus Status = ParticipantStatus.Registered,
    // Single attestation covers every player in the batch — the admin is
    // asserting they have consent from all of them under the same Source.
    // Null = no peer-share consent (e.g. simulated-roster test data).
    AdminRecordedPhoneShareConsent? AttestedConsent = null);

public record BulkAddParticipantsResultDto(int Added, int Skipped);

// ── Phase 7O additions: roster / divisions / teams / fixtures / match windows ──
//
// Several admin requests reuse DTOs already defined in CompetitionDtos.cs
// (AddParticipantRequest, UpdateParticipantStatusRequest, AssignParticipantTeamRequest,
// SetParticipantRoleRequest, SetParticipantDivisionRequest, SaveDivisionRequest,
// SaveTeamRequest, SaveFixtureRequest, SeedDivisionsFromPreviousRequest,
// SeedDivisionsResultDto). The records below are admin-only types not present in
// the player-facing surface.

/// <summary>
/// One row in the player-search results returned by
/// <c>ICompetitionAdminService.SearchPlayersAsync</c>. Tight subset of the Player
/// row — just enough for the add-participant search list.
/// </summary>
public record PlayerSearchResultDto(
    int PlayerId,
    string DisplayName,
    PlayerSex? Sex,
    DateTime? DateOfBirth,
    string? ClubName,
    bool IsLight);

/// <summary>
/// Set the division a team belongs to (or unassign with null). Distinct from
/// <see cref="SetParticipantDivisionRequest"/>, which sets a participant's
/// division — same shape, different semantics, kept as separate types so the
/// route surface reads naturally.
/// </summary>
public record AssignTeamDivisionRequest(int? CompetitionDivisionId);

/// <summary>
/// Assign (or unassign with <c>PlayerId == null</c>) a captain on a team.
/// Distinct from <c>SetTeamCaptainRequest</c> which only supports setting.
/// </summary>
public record AssignCaptainRequest(int CompetitionTeamId, int? PlayerId);

public record ValidateTeamResultDto(
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> Warnings);

/// <summary>
/// Pure-validator request: ask the service "would this assignment violate
/// eligibility rules?" without persisting. Lets the caller decide whether to
/// show a confirm dialog before saving.
/// </summary>
public record ConfirmFixtureAssignmentRequest(
    int? Player1Id,
    int? Player2Id,
    int? Player3Id,
    int? Player4Id,
    int? HomeTeamId,
    int? AwayTeamId);

public record ConfirmFixtureAssignmentResultDto(
    bool IsValid,
    IReadOnlyList<string> Violations);

public record SaveCalendarExceptionRequest(
    DateOnly ExceptionDate,
    string? Note,
    int? CompetitionDivisionId);

// ── Fixture reminder emails ───────────────────────────────────────────────────

public record CompetitionFixtureReminderDto(
    int CompetitionFixtureReminderId,
    int CompetitionId,
    int HoursBefore,
    string Subject,
    string Body,
    bool IncludeResultLink);

public record SaveFixtureReminderRequest(
    int HoursBefore,
    string Subject,
    string Body,
    bool IncludeResultLink);

public record SendFixtureReminderManualRequest(int CompetitionFixtureReminderId);

public record ScheduledReminderEmailDto(
    int CompetitionFixtureId,
    string FixtureLabel,
    DateTime ScheduledAt,
    int HoursBefore,
    DateTime SendAt,
    string Subject,
    bool AlreadySent);
