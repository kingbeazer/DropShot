using DropShot.Shared;
using DropShot.Shared.Dtos;

namespace DropShot.UI.Services;

/// <summary>
/// Admin-side competition operations for the CompetitionPage. Sibling of
/// <see cref="ICompetitionService"/>; that one is the player-facing read /
/// scoring surface, this one wraps every server-only dependency the
/// CompetitionPage previously injected directly (DbContext, ClubAuthorizationService,
/// UserManager, AdminEmailService, CompetitionSchedulerService,
/// FixtureSimulationService, ICompetitionRubberTemplateProvider).
///
/// Phase 7 PR 7N adds the read side + simple/independent writes (stages,
/// admins, clone, comp-save, email, rubber template). Phase 7 PR 7O adds the
/// coupled writes (participants, divisions, teams, match windows, fixtures,
/// scheduler, simulator).
/// </summary>
public interface ICompetitionAdminService
{
    // ── Read ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// One-shot fat aggregate for the CompetitionPage initial load. When
    /// <paramref name="competitionId"/> is null or 0, returns a create-mode
    /// payload (defaults + lookup tables, no entity data). When set, returns
    /// the full edit-mode payload (competition + stages + participants +
    /// divisions + teams + fixtures + match windows + court pairs + rubber
    /// template state + admins + clone-source candidates + lookup tables).
    /// Returns null in edit mode if the competition doesn't exist or the
    /// caller can't view it.
    /// </summary>
    Task<CompetitionEditDto?> GetCompetitionForEditAsync(int? competitionId, CancellationToken ct = default);

    /// <summary>
    /// Candidate competitions to seed divisions from: same format + host club,
    /// finished, has divisions. Used by the seed-divisions dialog after the
    /// host club changes mid-edit.
    /// </summary>
    Task<List<CompetitionSeedSourceDto>> GetSeedSourceCandidatesAsync(
        int? excludeCompetitionId, CompetitionFormat format, int? hostClubId, CancellationToken ct = default);

    Task<List<ClubSchedulingTemplateDto>> GetClubTemplatesAsync(int clubId, CancellationToken ct = default);
    Task<List<ClubEmailTemplateDto>> GetEmailTemplatesAsync(int clubId, CancellationToken ct = default);

    /// <summary>
    /// Whether the current user is allowed to edit the given competition.
    /// When <paramref name="competitionId"/> is null, returns whether the
    /// user can create any competition at all (subscribed user, club admin,
    /// or system admin).
    /// </summary>
    Task<bool> CanEditCompetitionAsync(int? competitionId, CancellationToken ct = default);

    Task<bool> IsSuperAdminAsync(CancellationToken ct = default);

    Task<List<CompetitionAdminRowDto>> GetCompetitionAdminsAsync(int competitionId, CancellationToken ct = default);

    // ── Competition lifecycle ────────────────────────────────────────────────

    /// <summary>
    /// Create or update a competition. Returns the saved competition's id (new
    /// or existing). Throws <see cref="InvalidOperationException"/> on duplicate
    /// name. When <paramref name="competitionId"/> is null, a new row is created
    /// and the creator user id is stamped on user-owned competitions (no host club).
    /// </summary>
    Task<int> SaveCompetitionAsync(
        int? competitionId, SaveCompetitionEditRequest request, CancellationToken ct = default);

    Task<CloneCompetitionResultDto> CloneCompetitionAsync(
        int sourceCompetitionId, CloneCompetitionRequest request, CancellationToken ct = default);

    /// <summary>Toggles the <c>IsStarted</c> flag and returns the new value.</summary>
    Task<bool> ToggleStartedAsync(int competitionId, CancellationToken ct = default);

    // ── Stages ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Bulk-add follow-on stages (e.g. SF + Final after a Knockout). Reorders
    /// the resulting stage list by natural type order.
    /// </summary>
    Task ApplyStageFollowUpAsync(
        int competitionId, ApplyStageFollowUpRequest request, CancellationToken ct = default);

    // ── Admins ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Add a competition-scoped admin by email. Throws
    /// <see cref="KeyNotFoundException"/> when no user with that email exists,
    /// and <see cref="InvalidOperationException"/> when the user is already an admin.
    /// </summary>
    Task AddCompetitionAdminAsync(
        int competitionId, AddCompetitionAdminRequest request, CancellationToken ct = default);

    Task RemoveCompetitionAdminAsync(int competitionId, string userId, CancellationToken ct = default);

    // ── Rubber template ──────────────────────────────────────────────────────

    Task<RubberTemplateStateDto> LoadRubberTemplateStateAsync(int competitionId, CancellationToken ct = default);

    /// <summary>
    /// Set the competition's rubber preset key (or clear it with a null key)
    /// and remove any custom template so the preset takes effect.
    /// </summary>
    Task ApplyRubberPresetAsync(
        int competitionId, ApplyRubberPresetRequest request, CancellationToken ct = default);

    /// <summary>
    /// Snapshot the currently-resolved template into a DB-backed custom one,
    /// so the user can edit individual rows. No-op if the resolved template is empty.
    /// </summary>
    Task ConvertToCustomTemplateAsync(int competitionId, CancellationToken ct = default);

    /// <summary>Append an empty row to the custom template, picking the next order.</summary>
    Task AddCustomRubberRowAsync(int competitionId, CancellationToken ct = default);

    /// <summary>
    /// Update a row in the custom template, identified by its (competition, order)
    /// pair. <see cref="SaveRubberRowRequest.RowId"/> is unused here for parity
    /// with the existing page UI which addresses rows by Order.
    /// </summary>
    Task SaveRubberRowAsync(
        int competitionId, SaveRubberRowRequest request, CancellationToken ct = default);

    Task DeleteCustomRubberRowAsync(int competitionId, int order, CancellationToken ct = default);

    /// <summary>Clear the custom template and the preset key — falls back to format default.</summary>
    Task RevertToDefaultTemplateAsync(int competitionId, CancellationToken ct = default);

    // ── Email ────────────────────────────────────────────────────────────────

    /// <summary>Send the configured email to every player on the named fixture.</summary>
    Task SendMatchEmailAsync(
        int competitionId, SendMatchEmailRequest request, CancellationToken ct = default);

    /// <summary>Send the configured email to every participant of the competition.</summary>
    Task SendCompetitionEmailAsync(
        int competitionId, SendCompetitionEmailRequest request, CancellationToken ct = default);

    // ── Participants ─────────────────────────────────────────────────────────

    /// <summary>
    /// Search the player pool for the add-participant flow. When the
    /// competition has a host club, results are restricted to that club's
    /// roster; otherwise the global player set is searched. Excludes players
    /// already enrolled in this competition. Honours competition gender / age
    /// restrictions.
    /// </summary>
    Task<List<PlayerSearchResultDto>> SearchPlayersAsync(
        int competitionId, SearchPlayersForAddRequest request, CancellationToken ct = default);

    Task AddParticipantAsync(int competitionId, AddParticipantRequest request, CancellationToken ct = default);

    /// <summary>
    /// Add a list of players to the competition in one round-trip. Players who
    /// are already enrolled or who push the roster over <c>MaxParticipants</c>
    /// are silently skipped; the result DTO reports counts.
    /// </summary>
    Task<BulkAddParticipantsResultDto> BulkAddParticipantsAsync(
        int competitionId, BulkAddParticipantsRequest request, CancellationToken ct = default);

    Task RemoveParticipantAsync(int competitionId, int playerId, CancellationToken ct = default);

    Task UpdateParticipantStatusAsync(
        int competitionId, int playerId, UpdateParticipantStatusRequest request, CancellationToken ct = default);

    Task AssignParticipantTeamAsync(
        int competitionId, int playerId, AssignParticipantTeamRequest request, CancellationToken ct = default);

    Task AssignParticipantRoleAsync(
        int competitionId, int playerId, SetParticipantRoleRequest request, CancellationToken ct = default);

    Task AssignParticipantDivisionAsync(
        int competitionId, int playerId, SetParticipantDivisionRequest request, CancellationToken ct = default);

    /// <summary>
    /// Set or update a participant's admin-entered initial Elo rating for this
    /// competition. Writes a <c>SeasonStart</c> snapshot — idempotent on re-call.
    /// Used to bootstrap a brand-new (non-cloned) competition before any Elo
    /// replay is possible.
    /// </summary>
    Task SetParticipantInitialRatingAsync(
        int competitionId, int playerId, SetParticipantInitialRatingRequest request, CancellationToken ct = default);

    /// <summary>
    /// Accept the Elo-replay rating suggestion for one participant. Writes the
    /// SeasonEnd snapshot on the previous competition and the SeasonStart on
    /// this one. Returns the suggestion that was applied, or null if there
    /// wasn't one (e.g. brand-new competition, or player didn't play in parent).
    /// </summary>
    Task<PlayerRatingSuggestionDto?> AcceptParticipantRatingAsync(
        int competitionId, int playerId, CancellationToken ct = default);

    /// <summary>
    /// Accept every visible rating suggestion in a single round. Returns the
    /// list of applied suggestions for UI confirmation.
    /// </summary>
    Task<List<PlayerRatingSuggestionDto>> AcceptAllParticipantRatingsAsync(
        int competitionId, CancellationToken ct = default);

    /// <summary>
    /// Apply the rating-driven division suggestion for one participant.
    /// </summary>
    Task ApplyDivisionPlacementAsync(
        int competitionId, int playerId, ApplyDivisionPlacementRequest request, CancellationToken ct = default);

    /// <summary>
    /// Apply the rating-driven role suggestion for one participant (TeamMatch).
    /// </summary>
    Task ApplyRolePlacementAsync(
        int competitionId, int playerId, ApplyRolePlacementRequest request, CancellationToken ct = default);

    /// <summary>
    /// Apply every pending division + role placement suggestion in one round.
    /// Returns the count of rows written.
    /// </summary>
    Task<int> ApplyAllPlacementsAsync(int competitionId, CancellationToken ct = default);

    /// <summary>
    /// Create a "light" Player (no user account) and immediately enrol them as
    /// a participant. Returns the new playerId. <see cref="CreateLightPlayerForCompetitionRequest.HostClubId"/>
    /// is required because light players are scoped to a club.
    /// </summary>
    Task<int> CreateLightPlayerAsync(
        int competitionId, CreateLightPlayerForCompetitionRequest request, CancellationToken ct = default);

    /// <summary>Update an existing light player's profile fields.</summary>
    Task SaveLightPlayerAsync(
        int competitionId, int playerId, SaveLightPlayerRequest request, CancellationToken ct = default);

    // ── Divisions ────────────────────────────────────────────────────────────

    /// <summary>
    /// Create or update a division. Returns the saved division's id. Also
    /// flips the competition's <c>HasDivisions</c> flag on first insert.
    /// </summary>
    Task<int> SaveDivisionAsync(
        int competitionId, int? divisionId, SaveDivisionRequest request, CancellationToken ct = default);

    /// <summary>
    /// Delete a division. Nullifies the division reference on participants and
    /// teams, deletes any division-scoped match windows, and removes the
    /// division row.
    /// </summary>
    Task DeleteDivisionAsync(int competitionId, int divisionId, CancellationToken ct = default);

    /// <summary>
    /// Seed divisions from a previously-completed competition: copy division
    /// rows, then assign participants by their previous-competition rank.
    /// When <c>ApplyPromotion</c> is true, also promote the top
    /// <c>PromoteCount</c> finishers up one division and demote the bottom
    /// <c>DemoteCount</c> down one.
    /// </summary>
    Task RunSeedDivisionsAsync(
        int competitionId, SeedDivisionsFromPreviousRequest request, CancellationToken ct = default);

    Task AssignTeamDivisionAsync(
        int competitionId, int teamId, AssignTeamDivisionRequest request, CancellationToken ct = default);

    // ── Teams ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Create or update a team's name. Captain and division are managed via
    /// dedicated methods (<see cref="AssignCaptainAsync"/> /
    /// <see cref="AssignTeamDivisionAsync"/>).
    /// </summary>
    Task<int> SaveTeamAsync(
        int competitionId, int? teamId, SaveTeamRequest request, CancellationToken ct = default);

    /// <summary>
    /// Delete a team. Nullifies <c>TeamId</c> on its participants (FK is
    /// no-action) and removes the team row.
    /// </summary>
    Task DeleteTeamAsync(int competitionId, int teamId, CancellationToken ct = default);

    /// <summary>
    /// Delete every team in the competition. Nullifies all
    /// participant/fixture team refs and deletes any rubbers attached to
    /// fixtures that lose both teams.
    /// </summary>
    Task DeleteAllTeamsAsync(int competitionId, CancellationToken ct = default);

    Task AssignCaptainAsync(
        int competitionId, AssignCaptainRequest request, CancellationToken ct = default);

    /// <summary>
    /// For every captain-less team, pick a random <c>FullPlayer</c> participant
    /// on that team and assign them as captain. Returns the number of teams
    /// updated.
    /// </summary>
    Task<int> AutoAssignCaptainsAsync(int competitionId, CancellationToken ct = default);

    Task<GenerateTeamsResultDto> GenerateTeamsPreviewAsync(
        int competitionId, GenerateTeamsRequest request, CancellationToken ct = default);

    Task ConfirmGenerateTeamsAsync(
        int competitionId, ConfirmGenerateTeamsRequest request, CancellationToken ct = default);

    /// <summary>
    /// Validate a team's role assignments: every required role from the
    /// competition's rubber template must be filled, with no duplicates and no
    /// blanks. Pure read — no mutations.
    /// </summary>
    Task<ValidateTeamResultDto> ValidateTeamAsync(
        int competitionId, int teamId, CancellationToken ct = default);

    // ── Fixtures ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Create or update a fixture. Caller is responsible for validation —
    /// call <see cref="ConfirmFixtureAssignmentAsync"/> first when the user
    /// has changed the assignment.
    /// </summary>
    Task<int> SaveFixtureAsync(
        int competitionId, int? fixtureId, SaveFixtureRequest request, CancellationToken ct = default);

    /// <summary>
    /// Delete a fixture. Cascades into its rubbers and saved-match rows. If
    /// the fixture has a recorded result, downstream knockout fixtures that
    /// inherited a winner from it are reset.
    /// </summary>
    Task DeleteFixtureAsync(int competitionId, int fixtureId, CancellationToken ct = default);

    /// <summary>
    /// Delete every fixture in the competition. Cascades into rubbers and
    /// saved matches.
    /// </summary>
    Task DeleteAllFixturesAsync(int competitionId, CancellationToken ct = default);

    /// <summary>
    /// Load a single fixture for the edit dialog. Returns null when the
    /// fixture doesn't exist or doesn't belong to this competition.
    /// </summary>
    Task<CompetitionFixtureDto?> LoadFixtureForDialogAsync(
        int competitionId, int fixtureId, CancellationToken ct = default);

    /// <summary>
    /// Validate a proposed fixture assignment without persisting. Returns
    /// <c>IsValid=true</c> when no eligibility / pairing rules are violated;
    /// otherwise the violation list explains why so the caller can prompt
    /// the user to override.
    /// </summary>
    Task<ConfirmFixtureAssignmentResultDto> ConfirmFixtureAssignmentAsync(
        int competitionId, ConfirmFixtureAssignmentRequest request, CancellationToken ct = default);

    /// <summary>
    /// Auto-schedule fixtures for the competition. Wraps
    /// <c>CompetitionSchedulerService</c>; the request's <c>DeleteExistingUnscheduled</c>
    /// flag maps to the scheduler's <c>UnscheduledOnly</c> delete mode.
    /// </summary>
    Task<ScheduleFixturesResultDto> ScheduleFixturesAsync(
        int competitionId, ScheduleFixturesAdminRequest request, CancellationToken ct = default);

    /// <summary>
    /// Generate plausible random results for every incomplete round-robin
    /// fixture. Super-admin only — wraps <c>FixtureSimulationService</c>.
    /// </summary>
    Task<SimulateRoundRobinResultDto> SimulateRoundRobinAsync(int competitionId, CancellationToken ct = default);

    /// <summary>
    /// Populate the first round of the knockout stage by ranking participants
    /// by their round-robin standings (or registration order when no RR
    /// results exist). Pairs top seeds against bottom seeds in bracket order.
    /// </summary>
    Task<SeedKnockoutFromStandingsResultDto> SeedKnockoutFromStandingsAsync(
        int competitionId, CancellationToken ct = default);

    // ── Match windows ────────────────────────────────────────────────────────

    Task<int> AddMatchWindowAsync(
        int competitionId, SaveMatchWindowRequest request, CancellationToken ct = default);

    Task DeleteMatchWindowAsync(int competitionId, int matchWindowId, CancellationToken ct = default);

    Task<int> AddDivisionMatchWindowAsync(
        int competitionId, int divisionId, SaveMatchWindowRequest request, CancellationToken ct = default);

    /// <summary>
    /// Append windows from a club scheduling template, skipping any (day,
    /// start, end) tuple already present. Returns the number of windows added.
    /// </summary>
    Task<int> ImportMatchWindowsFromTemplateAsync(
        int competitionId, ImportMatchWindowsFromTemplateRequest request, CancellationToken ct = default);

    // ── Calendar exceptions ──────────────────────────────────────────────────

    /// <summary>
    /// Add a calendar exception date to exclude from auto-scheduling. When
    /// <see cref="SaveCalendarExceptionRequest.CompetitionDivisionId"/> is null
    /// the exception applies competition-wide; when set it is division-scoped.
    /// Returns the new exception's id.
    /// </summary>
    Task<int> AddCalendarExceptionAsync(
        int competitionId, SaveCalendarExceptionRequest request, CancellationToken ct = default);

    Task DeleteCalendarExceptionAsync(int competitionId, int exceptionId, CancellationToken ct = default);

    // ── Fixture reminder emails ──────────────────────────────────────────────

    Task<List<CompetitionFixtureReminderDto>> GetFixtureRemindersAsync(int competitionId, CancellationToken ct = default);

    Task<List<ScheduledReminderEmailDto>> GetScheduledReminderEmailsAsync(int competitionId, CancellationToken ct = default);

    Task<int> SaveFixtureReminderAsync(
        int competitionId, int? reminderId, SaveFixtureReminderRequest request, CancellationToken ct = default);

    Task DeleteFixtureReminderAsync(int competitionId, int reminderId, CancellationToken ct = default);

    /// <summary>
    /// Manually send a reminder to all players in a specific fixture, bypassing
    /// the scheduled sweep timing. Admin-only. Also sends to fixtures that have
    /// already been sent this reminder.
    /// </summary>
    Task SendFixtureReminderManualAsync(
        int competitionId, int fixtureId, int reminderId, CancellationToken ct = default);
}
