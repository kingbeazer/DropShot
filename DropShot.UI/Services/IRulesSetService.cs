using DropShot.Shared.Dtos;

namespace DropShot.UI.Services;

/// <summary>
/// Rules-set domain abstraction. Read surface plus admin write surface
/// (phase 8c added the writes so the lifted RulesSets page can drop its
/// dependency on the MAUI-only ApiService).
/// </summary>
public interface IRulesSetService
{
    /// <summary>
    /// Returns rules sets, optionally filtered to a single club. Pass
    /// <paramref name="clubId"/> to scope the result; pass <c>null</c> to
    /// return all sets across all clubs (admin-only contexts).
    /// </summary>
    Task<List<RulesSetDto>> GetRulesSetsAsync(int? clubId = null, CancellationToken ct = default);
    Task<RulesSetDetailDto?> GetRulesSetAsync(int id, CancellationToken ct = default);

    /// <summary>
    /// Create (when <paramref name="id"/> is 0) or update a rules set.
    /// Returns the saved row.
    /// </summary>
    Task<RulesSetDto?> SaveRulesSetAsync(
        int id, SaveRulesSetRequest request, CancellationToken ct = default);

    Task DeleteRulesSetAsync(int id, CancellationToken ct = default);

    /// <summary>
    /// Append a new rule to a rules set; the next <c>SortOrder</c> is
    /// assigned automatically. Returns the created row.
    /// </summary>
    Task<RulesSetItemDto> AddRulesSetItemAsync(
        int rulesSetId, AddRulesSetItemRequest request, CancellationToken ct = default);

    Task DeleteRulesSetItemAsync(
        int rulesSetId, int itemId, CancellationToken ct = default);
}
