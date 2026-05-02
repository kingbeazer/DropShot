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
}
