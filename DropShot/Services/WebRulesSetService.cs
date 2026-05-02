using DropShot.Data;
using DropShot.Models;
using DropShot.Shared.Dtos;
using DropShot.UI.Services;
using Microsoft.EntityFrameworkCore;

namespace DropShot.Services;

/// <summary>
/// Web implementation of <see cref="IRulesSetService"/>. Mirrors
/// <c>RulesSetsController</c> — both read and admin-write endpoints.
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

    public async Task<RulesSetDto?> SaveRulesSetAsync(
        int id, SaveRulesSetRequest request, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        if (id == 0)
        {
            var r = new RulesSet { Name = request.Name.Trim(), Description = request.Description };
            db.RulesSets.Add(r);
            await db.SaveChangesAsync(ct);
            return new RulesSetDto(r.RulesSetId, r.Name, r.Description, 0);
        }
        else
        {
            var r = await db.RulesSets.Include(x => x.Items).FirstOrDefaultAsync(x => x.RulesSetId == id, ct)
                ?? throw new KeyNotFoundException("Rules set not found.");
            r.Name = request.Name.Trim();
            r.Description = request.Description;
            await db.SaveChangesAsync(ct);
            return new RulesSetDto(r.RulesSetId, r.Name, r.Description, r.Items.Count);
        }
    }

    public async Task DeleteRulesSetAsync(int id, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var r = await db.RulesSets.FindAsync([id], ct);
        if (r is null) return;
        db.RulesSets.Remove(r);
        await db.SaveChangesAsync(ct);
    }

    public async Task<RulesSetItemDto> AddRulesSetItemAsync(
        int rulesSetId, AddRulesSetItemRequest request, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var maxOrder = await db.RulesSetItems.Where(i => i.RulesSetId == rulesSetId)
            .Select(i => (int?)i.SortOrder).MaxAsync(ct) ?? 0;
        var item = new RulesSetItem
        {
            RulesSetId = rulesSetId,
            SortOrder  = maxOrder + 1,
            RuleText   = request.RuleText.Trim(),
        };
        db.RulesSetItems.Add(item);
        await db.SaveChangesAsync(ct);
        return new RulesSetItemDto(item.RulesSetItemId, item.RulesSetId, item.SortOrder, item.RuleText);
    }

    public async Task DeleteRulesSetItemAsync(int rulesSetId, int itemId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var item = await db.RulesSetItems.FindAsync([itemId], ct);
        if (item is null || item.RulesSetId != rulesSetId) return;
        db.RulesSetItems.Remove(item);
        await db.SaveChangesAsync(ct);
    }
}
