using DropShot.Shared;
using DropShot.Shared.Dtos;

namespace DropShot.UI.Services;

/// <summary>
/// Competition domain abstraction. Seeded with the read surface needed by
/// ViewCompetition, LeagueTable, and DisplayControl (phase 4); setup/scoring
/// write methods land in phases 5–7.
/// </summary>
public interface ICompetitionService
{
    Task<List<CompetitionDto>> GetCompetitionsAsync(bool includeArchived = false, CancellationToken ct = default);
    Task<CompetitionDetailDto?> GetCompetitionAsync(int id, CancellationToken ct = default);

    /// <summary>
    /// Register the authenticated user as a participant in the competition.
    /// Server resolves the player from the authenticated user's <c>UserId</c>;
    /// returns a 400-equivalent on web (KeyNotFoundException) when no player
    /// record exists for the user, or already-registered.
    /// </summary>
    Task SelfRegisterAsync(int competitionId, ParticipantStatus status, CancellationToken ct = default);

    /// <summary>
    /// Upgrade the authenticated user's participation status (typically from
    /// Registered → FullPlayer or Substitute).
    /// </summary>
    Task ConfirmParticipationAsync(int competitionId, ParticipantStatus status, CancellationToken ct = default);

    /// <summary>
    /// Approve a fixture's awaiting-verification result. Without
    /// <c>OverrideScores</c> the existing submission is accepted as-is; with
    /// it, the original result is preserved for audit and the new score
    /// becomes authoritative. Marks Completed + runs CompetitionProgressionService.
    /// </summary>
    Task ApproveFixtureResultAsync(int fixtureId, ApproveFixtureResultRequest request, CancellationToken ct = default);
}
