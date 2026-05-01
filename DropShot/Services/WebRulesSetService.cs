using DropShot.Data;
using DropShot.Shared.Dtos;
using DropShot.UI.Services;
using Microsoft.EntityFrameworkCore;

namespace DropShot.Services;

/// <summary>
/// Web implementation of <see cref="IRulesSetService"/>. Mirrors
/// <c>RulesSetsController</c> read endpoints. Phase 3 seed: read surface only.
/// </summary>
public sealed class WebRulesSetService(IDbContextFactory<MyDbContext> dbFactory) : IRulesSetService
{
    public async Task<List<RulesSetDto>> GetRulesSetsAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var sets = await db.RulesSets.Include(r => r.Items).OrderBy(r => r.Name).ToListAsync(ct);
        return sets.Select(r => new RulesSetDto(r.RulesSetId, r.Name, r.Description, r.Items.Count)).ToList();
    }

    public async Task<RulesSetDetailDto?> GetRulesSetAsync(int id, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var r = await db.RulesSets.Include(x => x.Items.OrderBy(i => i.SortOrder))
            .FirstOrDefaultAsync(x => x.RulesSetId == id, ct);
        if (r is null) return null;

        return new RulesSetDetailDto(r.RulesSetId, r.Name, r.Description,
            r.Items.Select(i => new RulesSetItemDto(i.RulesSetItemId, i.RulesSetId, i.SortOrder, i.RuleText)).ToList());
    }
}
