using DropShot.Shared.Dtos;

namespace DropShot.UI.Services;

/// <summary>
/// Rules-set domain abstraction. Read surface only at phase 3; admin write
/// surface lands in phase 5 alongside Admin/SiteSettings.
/// </summary>
public interface IRulesSetService
{
    Task<List<RulesSetDto>> GetRulesSetsAsync(CancellationToken ct = default);
    Task<RulesSetDetailDto?> GetRulesSetAsync(int id, CancellationToken ct = default);
}
