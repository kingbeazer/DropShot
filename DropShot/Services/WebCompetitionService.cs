using DropShot.Data;
using DropShot.Models;
using DropShot.Shared.Dtos;
using DropShot.UI.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace DropShot.Services;

/// <summary>
/// Web implementation of <see cref="ICompetitionService"/>. Mirrors
/// <c>CompetitionsController</c> read endpoints, including the
/// visibility filter via <see cref="ClubAuthorizationService"/>. Phase 3 seed:
/// read surface only.
/// </summary>
public sealed class WebCompetitionService(
    IDbContextFactory<MyDbContext> dbFactory,
    ClubAuthorizationService authzService,
    IHttpContextAccessor httpContextAccessor) : ICompetitionService
{
    public async Task<List<CompetitionDto>> GetCompetitionsAsync(bool includeArchived = false, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var baseQuery = db.Competition
            .Include(c => c.HostClub)
            .Include(c => c.Rules)
            .Include(c => c.Event)
            .AsQueryable();

        if (!includeArchived)
            baseQuery = baseQuery.Where(c => !c.IsArchived);

        var user = httpContextAccessor.HttpContext?.User ?? new ClaimsPrincipal();
        var visCtx = await authzService.GetVisibilityContextAsync(user);
        var query = authzService.ApplyVisibilityFilter(baseQuery, db, visCtx);

        var comps = await query
            .OrderBy(c => c.CompetitionName)
            .ToListAsync(ct);
        return comps.Select(ToDto).ToList();
    }

    public async Task<CompetitionDetailDto?> GetCompetitionAsync(int id, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var c = await db.Competition
            .Include(x => x.HostClub)
            .Include(x => x.Rules)
            .Include(x => x.Event)
            .Include(x => x.Stages.OrderBy(s => s.StageOrder))
            .Include(x => x.Participants).ThenInclude(p => p.Player)
            .Include(x => x.Participants).ThenInclude(p => p.Team)
            .Include(x => x.Participants).ThenInclude(p => p.Division)
            .Include(x => x.Divisions)
            .Include(x => x.AllowedPlayers)
            .FirstOrDefaultAsync(x => x.CompetitionID == id, ct);

        if (c is null) return null;

        var user = httpContextAccessor.HttpContext?.User ?? new ClaimsPrincipal();
        if (!await authzService.CanViewCompetitionAsync(user, id))
            return null;

        return new CompetitionDetailDto(
            c.CompetitionID, c.CompetitionName,
            (DropShot.Shared.CompetitionFormat)c.CompetitionFormat,
            c.MaxParticipants, c.StartDate, c.EndDate, c.MaxAge, c.MinAge,
            (DropShot.Shared.PlayerSex?)c.EligibleSex,
            c.HostClubId, c.HostClub?.Name, c.RulesSetId, c.Rules?.Name,
            c.EventId, c.Event?.Name,
            c.Stages.Select(s => new CompetitionStageDto(
                s.CompetitionStageId, s.Name, s.StageOrder,
                (DropShot.Shared.StageType)s.StageType)).ToList(),
            c.Participants.Select(p => new CompetitionParticipantDto(
                p.PlayerId, p.Player?.DisplayName ?? "",
                (DropShot.Shared.ParticipantStatus)p.Status,
                p.RegisteredAt, p.TeamId, p.Team?.Name,
                p.Player?.MobileNumber,
                p.Role,
                (DropShot.Shared.PlayerSex?)p.Player?.Sex,
                p.CompetitionDivisionId,
                p.Division?.Name)).ToList(),
            c.IsArchived,
            c.IsStarted,
            c.CreatorUserId,
            c.IsRestricted,
            c.AllowedPlayers.Select(ap => ap.PlayerId).ToList(),
            c.HasDivisions,
            c.Divisions.OrderBy(d => d.Rank)
                .Select(d => new CompetitionDivisionDto(d.CompetitionDivisionId, d.CompetitionId, d.Rank, d.Name))
                .ToList());
    }

    private static CompetitionDto ToDto(Competition c) => new(
        c.CompetitionID, c.CompetitionName,
        (DropShot.Shared.CompetitionFormat)c.CompetitionFormat,
        c.MaxParticipants, c.StartDate, c.EndDate, c.MaxAge, c.MinAge,
        (DropShot.Shared.PlayerSex?)c.EligibleSex,
        c.HostClubId, c.HostClub?.Name, c.RulesSetId, c.Rules?.Name,
        c.EventId, c.Event?.Name, c.IsArchived, c.IsStarted,
        c.CreatorUserId, c.IsRestricted);
}
