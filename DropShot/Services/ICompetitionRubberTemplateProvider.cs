using DropShot.Data;
using DropShot.Models;
using Microsoft.EntityFrameworkCore;

namespace DropShot.Services;

public interface ICompetitionRubberTemplateProvider
{
    /// <summary>
    /// Resolve the active rubber template for a competition. Priority: DB-backed
    /// <see cref="CompetitionRubberTemplate"/> (if present) → preset keyed by
    /// <see cref="Competition.RubberTemplateKey"/> → default for <see cref="Competition.CompetitionFormat"/>.
    /// </summary>
    Task<IReadOnlyList<RubberDef>?> GetAsync(MyDbContext db, int competitionId);

    Task<IReadOnlyList<string>> GetRoleSetAsync(MyDbContext db, int competitionId);
}

public class CompetitionRubberTemplateProvider : ICompetitionRubberTemplateProvider
{
    public async Task<IReadOnlyList<RubberDef>?> GetAsync(MyDbContext db, int competitionId)
    {
        var comp = await db.Competition
            .AsNoTracking()
            .Include(c => c.RubberTemplate)
                .ThenInclude(t => t!.Rubbers)
            .FirstOrDefaultAsync(c => c.CompetitionID == competitionId);

        if (comp is null) return null;

        // 1. DB-backed custom template
        if (comp.RubberTemplate is { Rubbers.Count: > 0 } customTemplate)
        {
            return customTemplate.Rubbers
                .OrderBy(r => r.Order)
                .Select(r => new RubberDef(r.Order, r.Name, r.CourtNumber, r.HomeRoles, r.AwayRoles))
                .ToList();
        }

        // 2. Keyed preset or format default
        return RubberTemplateRegistry.Resolve(comp.CompetitionFormat, comp.RubberTemplateKey);
    }

    public async Task<IReadOnlyList<string>> GetRoleSetAsync(MyDbContext db, int competitionId)
    {
        var template = await GetAsync(db, competitionId);
        return RubberTemplateRegistry.GetRoleSet(template);
    }
}
