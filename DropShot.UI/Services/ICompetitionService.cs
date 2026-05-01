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
}
