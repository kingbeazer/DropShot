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
            .AsSplitQuery()
            .AsNoTracking()
            .Include(x => x.HostClub)
            .Include(x => x.Rules)
            .Include(x => x.Event)
            .Include(x => x.Stages.OrderBy(s => s.StageOrder))
            .Include(x => x.Participants).ThenInclude(p => p.Player)
            .Include(x => x.Participants).ThenInclude(p => p.Team)
            .Include(x => x.Participants).ThenInclude(p => p.Division)
            .Include(x => x.Divisions)
            .Include(x => x.AllowedPlayers)
            .Include(x => x.Fixtures).ThenInclude(f => f.Stage)
            .Include(x => x.Fixtures).ThenInclude(f => f.Court)
            .Include(x => x.Fixtures).ThenInclude(f => f.CourtPair).ThenInclude(cp => cp!.Court1)
            .Include(x => x.Fixtures).ThenInclude(f => f.CourtPair).ThenInclude(cp => cp!.Court2)
            .Include(x => x.Fixtures).ThenInclude(f => f.Player1)
            .Include(x => x.Fixtures).ThenInclude(f => f.Player2)
            .Include(x => x.Fixtures).ThenInclude(f => f.Player3)
            .Include(x => x.Fixtures).ThenInclude(f => f.Player4)
            .Include(x => x.Fixtures).ThenInclude(f => f.HomeTeam)
            .Include(x => x.Fixtures).ThenInclude(f => f.AwayTeam)
            .Include(x => x.Fixtures).ThenInclude(f => f.Rubbers).ThenInclude(r => r.HomePlayer1)
            .Include(x => x.Fixtures).ThenInclude(f => f.Rubbers).ThenInclude(r => r.HomePlayer2)
            .Include(x => x.Fixtures).ThenInclude(f => f.Rubbers).ThenInclude(r => r.AwayPlayer1)
            .Include(x => x.Fixtures).ThenInclude(f => f.Rubbers).ThenInclude(r => r.AwayPlayer2)
            .Include(x => x.Teams).ThenInclude(t => t.Captain)
            .FirstOrDefaultAsync(x => x.CompetitionID == id, ct);

        if (c is null) return null;

        var user = httpContextAccessor.HttpContext?.User ?? new ClaimsPrincipal();
        if (!await authzService.CanViewCompetitionAsync(user, id))
            return null;

        var courtPairs = await db.CourtPairs
            .AsNoTracking()
            .Where(cp => cp.CompetitionId == id)
            .Include(cp => cp.Court1)
            .Include(cp => cp.Court2)
            .OrderBy(cp => cp.Name)
            .ToListAsync(ct);

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
                .ToList(),
            c.Fixtures
                .OrderBy(f => f.RoundNumber).ThenBy(f => f.ScheduledAt)
                .Select(ToFixtureDto)
                .ToList(),
            c.Teams
                .Select(t => new CompetitionTeamDto(
                    t.CompetitionTeamId, t.CompetitionId, t.Name,
                    t.CaptainPlayerId, t.Captain?.DisplayName))
                .ToList(),
            courtPairs.Select(cp => new CourtPairDto(
                cp.CourtPairId, cp.CompetitionId,
                cp.Court1Id, cp.Court1.Name,
                cp.Court2Id, cp.Court2.Name,
                cp.Name)).ToList());
    }

    private static CompetitionFixtureDto ToFixtureDto(DropShot.Models.CompetitionFixture f) => new(
        f.CompetitionFixtureId, f.CompetitionId,
        f.CompetitionStageId, f.Stage?.Name,
        f.CourtId, f.Court?.Name,
        f.ScheduledAt, (DropShot.Shared.FixtureStatus)f.Status,
        f.FixtureLabel, f.RoundNumber,
        f.Player1Id, f.Player1?.DisplayName,
        f.Player2Id, f.Player2?.DisplayName,
        f.Player3Id, f.Player3?.DisplayName,
        f.Player4Id, f.Player4?.DisplayName,
        f.ResultSummary, f.WinnerPlayerId,
        f.HomeTeamId, f.HomeTeam?.Name,
        f.AwayTeamId, f.AwayTeam?.Name,
        f.WinnerTeamId, f.CourtPairId, f.CourtPair?.Name,
        f.Rubbers
            .OrderBy(r => r.Order)
            .Select(r => new RubberDto(
                r.RubberId, r.CompetitionFixtureId, r.Order, r.Name, r.CourtNumber,
                r.HomeRoles, r.AwayRoles,
                r.HomePlayer1Id, r.HomePlayer1?.DisplayName,
                r.HomePlayer2Id, r.HomePlayer2?.DisplayName,
                r.AwayPlayer1Id, r.AwayPlayer1?.DisplayName,
                r.AwayPlayer2Id, r.AwayPlayer2?.DisplayName,
                r.HomeGames, r.AwayGames, r.WinnerTeamId,
                r.IsComplete, r.SavedMatchId))
            .ToList());

    private static CompetitionDto ToDto(Competition c) => new(
        c.CompetitionID, c.CompetitionName,
        (DropShot.Shared.CompetitionFormat)c.CompetitionFormat,
        c.MaxParticipants, c.StartDate, c.EndDate, c.MaxAge, c.MinAge,
        (DropShot.Shared.PlayerSex?)c.EligibleSex,
        c.HostClubId, c.HostClub?.Name, c.RulesSetId, c.Rules?.Name,
        c.EventId, c.Event?.Name, c.IsArchived, c.IsStarted,
        c.CreatorUserId, c.IsRestricted);
}
