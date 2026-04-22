using DropShot.Data;
using DropShot.Models;
using DropShot.Services;
using DropShot.Shared.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DropShot.Controllers;

[ApiController]
[Route("api")]
[Authorize(AuthenticationSchemes = "Bearer")]
public class LeaguesController(
    IDbContextFactory<MyDbContext> dbFactory,
    ClubAuthorizationService authzService) : ControllerBase
{
    // ── Leagues ───────────────────────────────────────────────────────────

    [HttpGet("leagues")]
    public async Task<ActionResult<List<LeagueSummaryDto>>> List([FromQuery] int? clubId = null, [FromQuery] bool includeArchived = false)
    {
        await using var db = dbFactory.CreateDbContext();
        var query = db.Leagues.AsNoTracking().Include(l => l.HostClub).AsQueryable();
        if (clubId.HasValue) query = query.Where(l => l.HostClubId == clubId.Value);
        if (!includeArchived) query = query.Where(l => !l.IsArchived);

        return await query
            .OrderBy(l => l.Name)
            .Select(l => new LeagueSummaryDto(
                l.LeagueId,
                l.Name,
                l.HostClubId,
                l.HostClub.Name,
                l.IsArchived,
                l.Seasons.Count,
                l.Memberships.Count(m => m.IsActive)))
            .ToListAsync();
    }

    [HttpGet("leagues/{id:int}")]
    public async Task<ActionResult<LeagueDetailDto>> Get(int id)
    {
        await using var db = dbFactory.CreateDbContext();
        var l = await db.Leagues.AsNoTracking().Include(x => x.HostClub)
            .FirstOrDefaultAsync(x => x.LeagueId == id);
        if (l is null) return NotFound();
        return ToDetailDto(l);
    }

    [HttpPost("leagues")]
    public async Task<ActionResult<LeagueDetailDto>> Create([FromBody] CreateLeagueRequest req)
    {
        await using var db = dbFactory.CreateDbContext();
        if (!await authzService.CanCreateClubCompetitionAsync(User, req.HostClubId)) return Forbid();

        if (string.IsNullOrWhiteSpace(req.Name))
            return BadRequest(new { message = "Name is required." });

        var league = new League
        {
            Name = req.Name.Trim(),
            HostClubId = req.HostClubId,
            CompetitionFormat = (Models.CompetitionFormat)req.CompetitionFormat,
            TeamSize = req.TeamSize,
            LeagueScoring = (Models.LeagueScoringMode)req.LeagueScoring,
            RubberTemplateKey = req.RubberTemplateKey,
            MatchFormat = (Models.MatchFormatType)req.MatchFormat,
            NumberOfSets = req.NumberOfSets,
            GamesPerSet = req.GamesPerSet,
            SetWinMode = (Models.SetWinMode)req.SetWinMode,
            TeamsPerDivisionTarget = req.TeamsPerDivisionTarget,
            TeamsPerDivisionMin = req.TeamsPerDivisionMin,
        };
        db.Leagues.Add(league);
        await db.SaveChangesAsync();

        await db.Entry(league).Reference(l => l.HostClub).LoadAsync();
        return CreatedAtAction(nameof(Get), new { id = league.LeagueId }, ToDetailDto(league));
    }

    [HttpPut("leagues/{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateLeagueRequest req)
    {
        await using var db = dbFactory.CreateDbContext();
        var l = await db.Leagues.FindAsync(id);
        if (l is null) return NotFound();
        if (!await authzService.CanEditCompetitionAsync(User, l.HostClubId)) return Forbid();

        l.Name = req.Name.Trim();
        l.CompetitionFormat = (Models.CompetitionFormat)req.CompetitionFormat;
        l.TeamSize = req.TeamSize;
        l.LeagueScoring = (Models.LeagueScoringMode)req.LeagueScoring;
        l.RubberTemplateKey = req.RubberTemplateKey;
        l.MatchFormat = (Models.MatchFormatType)req.MatchFormat;
        l.NumberOfSets = req.NumberOfSets;
        l.GamesPerSet = req.GamesPerSet;
        l.SetWinMode = (Models.SetWinMode)req.SetWinMode;
        l.TeamsPerDivisionTarget = req.TeamsPerDivisionTarget;
        l.TeamsPerDivisionMin = req.TeamsPerDivisionMin;
        l.IsArchived = req.IsArchived;
        await db.SaveChangesAsync();
        return NoContent();
    }

    // ── Memberships ───────────────────────────────────────────────────────

    [HttpGet("leagues/{id:int}/memberships")]
    public async Task<ActionResult<List<LeagueMembershipDto>>> GetMemberships(int id, [FromQuery] bool includeInactive = false)
    {
        await using var db = dbFactory.CreateDbContext();
        var q = db.LeagueMemberships.AsNoTracking().Include(m => m.Player)
            .Where(m => m.LeagueId == id);
        if (!includeInactive) q = q.Where(m => m.IsActive);

        return await q
            .OrderBy(m => m.Player.DisplayName)
            .Select(m => new LeagueMembershipDto(
                m.PlayerId, m.Player.DisplayName, m.JoinedAt, m.IsActive, m.CurrentDivisionRank))
            .ToListAsync();
    }

    [HttpPost("leagues/{id:int}/memberships")]
    public async Task<IActionResult> EnrolPlayer(int id, [FromBody] EnrolPlayerRequest req)
    {
        await using var db = dbFactory.CreateDbContext();
        var league = await db.Leagues.FindAsync(id);
        if (league is null) return NotFound();
        if (!await authzService.CanEditCompetitionAsync(User, league.HostClubId)) return Forbid();

        var existing = await db.LeagueMemberships.FindAsync(id, req.PlayerId);
        if (existing is null)
        {
            db.LeagueMemberships.Add(new LeagueMembership { LeagueId = id, PlayerId = req.PlayerId });
        }
        else if (!existing.IsActive)
        {
            existing.IsActive = true;
        }
        else
        {
            return Ok(); // already enrolled
        }
        await db.SaveChangesAsync();
        return Ok();
    }

    [HttpDelete("leagues/{id:int}/memberships/{playerId:int}")]
    public async Task<IActionResult> UnenrolPlayer(int id, int playerId)
    {
        await using var db = dbFactory.CreateDbContext();
        var league = await db.Leagues.FindAsync(id);
        if (league is null) return NotFound();
        if (!await authzService.CanEditCompetitionAsync(User, league.HostClubId)) return Forbid();

        var m = await db.LeagueMemberships.FindAsync(id, playerId);
        if (m is null) return NotFound();
        m.IsActive = false;
        await db.SaveChangesAsync();
        return NoContent();
    }

    // ── Seasons ───────────────────────────────────────────────────────────

    [HttpGet("leagues/{id:int}/seasons")]
    public async Task<ActionResult<List<LeagueSeasonSummaryDto>>> GetSeasons(int id)
    {
        await using var db = dbFactory.CreateDbContext();
        return await db.LeagueSeasons.AsNoTracking()
            .Where(s => s.LeagueId == id)
            .OrderByDescending(s => s.StartDate)
            .Select(s => new LeagueSeasonSummaryDto(
                s.LeagueSeasonId, s.LeagueId, s.Name, s.StartDate, s.EndDate,
                (LeagueSeasonStatusDto)s.Status, s.ClosedAt, s.Divisions.Count))
            .ToListAsync();
    }

    [HttpPost("leagues/{id:int}/seasons")]
    public async Task<ActionResult<LeagueSeasonSummaryDto>> CreateSeason(int id, [FromBody] CreateSeasonRequest req)
    {
        await using var db = dbFactory.CreateDbContext();
        var league = await db.Leagues.FindAsync(id);
        if (league is null) return NotFound();
        if (!await authzService.CanEditCompetitionAsync(User, league.HostClubId)) return Forbid();

        bool openSeasonExists = await db.LeagueSeasons
            .AnyAsync(s => s.LeagueId == id && s.Status != LeagueSeasonStatus.Closed);
        if (openSeasonExists)
            return BadRequest(new { message = "Close the existing open season before starting a new one." });

        if (string.IsNullOrWhiteSpace(req.Name))
            return BadRequest(new { message = "Season name is required." });

        var season = new LeagueSeason
        {
            LeagueId = id,
            Name = req.Name.Trim(),
            StartDate = req.StartDate,
            EndDate = req.EndDate,
            Status = LeagueSeasonStatus.Planning,
        };
        db.LeagueSeasons.Add(season);
        await db.SaveChangesAsync();
        return new LeagueSeasonSummaryDto(
            season.LeagueSeasonId, season.LeagueId, season.Name, season.StartDate, season.EndDate,
            (LeagueSeasonStatusDto)season.Status, null, 0);
    }

    [HttpGet("seasons/{id:int}")]
    public async Task<ActionResult<LeagueSeasonDetailDto>> GetSeason(int id)
    {
        await using var db = dbFactory.CreateDbContext();
        var season = await db.LeagueSeasons.AsNoTracking()
            .Include(s => s.Divisions).ThenInclude(d => d.Competition).ThenInclude(c => c.Teams)
            .Include(s => s.Divisions).ThenInclude(d => d.Competition).ThenInclude(c => c.Participants)
            .FirstOrDefaultAsync(s => s.LeagueSeasonId == id);
        if (season is null) return NotFound();

        var divisions = season.Divisions
            .OrderBy(d => d.Rank)
            .Select(d => new LeagueDivisionDto(
                d.LeagueDivisionId, d.Rank, d.Name, d.CompetitionId,
                d.Competition.CompetitionName,
                d.Competition.Teams.Count,
                d.Competition.Participants.Count(p => p.Status == Models.ParticipantStatus.Registered
                                                   || p.Status == Models.ParticipantStatus.Confirmed)))
            .ToList();

        return new LeagueSeasonDetailDto(
            season.LeagueSeasonId, season.LeagueId, season.Name, season.StartDate, season.EndDate,
            (LeagueSeasonStatusDto)season.Status, season.ClosedAt, divisions);
    }

    [HttpPost("seasons/{id:int}/suggest-divisions")]
    public async Task<ActionResult<DivisionSuggestionDto>> SuggestDivisions(int id)
    {
        await using var db = dbFactory.CreateDbContext();
        var season = await db.LeagueSeasons
            .Include(s => s.League).ThenInclude(l => l.Memberships).ThenInclude(m => m.Player)
            .FirstOrDefaultAsync(s => s.LeagueSeasonId == id);
        if (season is null) return NotFound();
        if (!await authzService.CanEditCompetitionAsync(User, season.League.HostClubId)) return Forbid();

        var activeMembers = season.League.Memberships
            .Where(m => m.IsActive)
            .OrderBy(m => m.CurrentDivisionRank ?? byte.MaxValue)
            .ThenBy(m => m.Player.DisplayName)
            .ToList();

        int playersPerDivision = season.League.TeamsPerDivisionTarget * season.League.TeamSize;
        if (playersPerDivision <= 0) playersPerDivision = 32;
        int divisionCount = Math.Max(1, (int)Math.Ceiling(activeMembers.Count / (double)playersPerDivision));

        // Bucket: if any members have CurrentDivisionRank set (returning season), group by rank.
        // Otherwise split by target size in order.
        List<DivisionBucketDto> buckets;
        var unassigned = new List<int>();

        bool hasRanks = activeMembers.Any(m => m.CurrentDivisionRank.HasValue);
        if (hasRanks)
        {
            buckets = [];
            for (byte rank = 1; rank <= divisionCount; rank++)
            {
                var inRank = activeMembers
                    .Where(m => (m.CurrentDivisionRank ?? (byte)divisionCount) == rank)
                    .Select(m => m.PlayerId)
                    .ToList();
                buckets.Add(new DivisionBucketDto(rank, $"Division {rank}", inRank));
            }
            // Members with no rank (new joiners): append to bottom division
            var newbies = activeMembers
                .Where(m => !m.CurrentDivisionRank.HasValue)
                .Select(m => m.PlayerId)
                .ToList();
            if (newbies.Count > 0 && buckets.Count > 0)
            {
                var bottom = buckets[^1];
                buckets[^1] = bottom with { PlayerIds = bottom.PlayerIds.Concat(newbies).ToList() };
            }
        }
        else
        {
            buckets = [];
            int index = 0;
            for (byte rank = 1; rank <= divisionCount; rank++)
            {
                var slice = activeMembers.Skip(index).Take(playersPerDivision).Select(m => m.PlayerId).ToList();
                buckets.Add(new DivisionBucketDto(rank, $"Division {rank}", slice));
                index += playersPerDivision;
            }
        }

        return new DivisionSuggestionDto(buckets, unassigned);
    }

    [HttpPost("seasons/{id:int}/divisions")]
    public async Task<IActionResult> CreateDivisions(int id, [FromBody] CreateDivisionsRequest req)
    {
        await using var db = dbFactory.CreateDbContext();
        var season = await db.LeagueSeasons
            .Include(s => s.League)
            .Include(s => s.Divisions)
            .FirstOrDefaultAsync(s => s.LeagueSeasonId == id);
        if (season is null) return NotFound();
        if (!await authzService.CanEditCompetitionAsync(User, season.League.HostClubId)) return Forbid();
        if (season.Status != LeagueSeasonStatus.Planning)
            return BadRequest(new { message = "Divisions can only be created while the season is in Planning." });

        if (season.Divisions.Any())
            return BadRequest(new { message = "Divisions already exist for this season." });

        var league = season.League;
        foreach (var d in req.Divisions.OrderBy(x => x.Rank))
        {
            var comp = new Competition
            {
                CompetitionName = $"{league.Name} — {season.Name} — {d.Name}",
                CompetitionFormat = league.CompetitionFormat,
                TeamSize = league.TeamSize,
                LeagueScoring = league.LeagueScoring,
                RubberTemplateKey = league.RubberTemplateKey,
                MatchFormat = league.MatchFormat,
                NumberOfSets = league.NumberOfSets,
                GamesPerSet = league.GamesPerSet,
                SetWinMode = league.SetWinMode,
                HostClubId = league.HostClubId,
                StartDate = season.StartDate,
                EndDate = season.EndDate,
                IsStarted = false,
            };
            db.Competition.Add(comp);
            await db.SaveChangesAsync();

            // Teams + participants
            foreach (var t in d.Teams)
            {
                var team = new CompetitionTeam { CompetitionId = comp.CompetitionID, Name = t.TeamName };
                db.CompetitionTeams.Add(team);
                await db.SaveChangesAsync();

                foreach (var m in t.Members)
                {
                    db.CompetitionParticipants.Add(new CompetitionParticipant
                    {
                        CompetitionId = comp.CompetitionID,
                        PlayerId = m.PlayerId,
                        TeamId = team.CompetitionTeamId,
                        Role = m.Role,
                        Status = Models.ParticipantStatus.Confirmed,
                    });
                }
            }
            await db.SaveChangesAsync();

            var division = new LeagueDivision
            {
                LeagueSeasonId = season.LeagueSeasonId,
                Rank = d.Rank,
                Name = d.Name,
                CompetitionId = comp.CompetitionID,
            };
            db.LeagueDivisions.Add(division);
            await db.SaveChangesAsync();

            comp.LeagueDivisionId = division.LeagueDivisionId;
            await db.SaveChangesAsync();
        }

        return NoContent();
    }

    [HttpPost("seasons/{id:int}/activate")]
    public async Task<IActionResult> ActivateSeason(int id)
    {
        await using var db = dbFactory.CreateDbContext();
        var season = await db.LeagueSeasons
            .Include(s => s.League)
            .Include(s => s.Divisions).ThenInclude(d => d.Competition)
            .FirstOrDefaultAsync(s => s.LeagueSeasonId == id);
        if (season is null) return NotFound();
        if (!await authzService.CanEditCompetitionAsync(User, season.League.HostClubId)) return Forbid();
        if (season.Status == LeagueSeasonStatus.Closed)
            return BadRequest(new { message = "Season is already closed." });

        season.Status = LeagueSeasonStatus.Active;
        foreach (var d in season.Divisions)
            d.Competition.IsStarted = true;
        await db.SaveChangesAsync();
        return NoContent();
    }

    // ── Stats ─────────────────────────────────────────────────────────────

    [HttpGet("seasons/{id:int}/stats")]
    public async Task<ActionResult<List<PlayerStatsDto>>> GetSeasonStats(int id)
    {
        await using var db = dbFactory.CreateDbContext();
        var season = await db.LeagueSeasons
            .Include(s => s.League)
            .Include(s => s.Divisions).ThenInclude(d => d.Competition).ThenInclude(c => c.Participants).ThenInclude(p => p.Player)
            .FirstOrDefaultAsync(s => s.LeagueSeasonId == id);
        if (season is null) return NotFound();

        var rows = new List<PlayerStatsDto>();
        foreach (var d in season.Divisions.OrderBy(d => d.Rank))
        {
            rows.AddRange(await BuildStatsForDivision(db, d, season.League.LeagueScoring));
        }
        return rows;
    }

    [HttpGet("divisions/{id:int}/stats")]
    public async Task<ActionResult<List<PlayerStatsDto>>> GetDivisionStats(int id)
    {
        await using var db = dbFactory.CreateDbContext();
        var division = await db.LeagueDivisions
            .Include(d => d.Season).ThenInclude(s => s.League)
            .Include(d => d.Competition).ThenInclude(c => c.Participants).ThenInclude(p => p.Player)
            .FirstOrDefaultAsync(d => d.LeagueDivisionId == id);
        if (division is null) return NotFound();

        return (await BuildStatsForDivision(db, division, division.Season.League.LeagueScoring)).ToList();
    }

    private static async Task<IEnumerable<PlayerStatsDto>> BuildStatsForDivision(
        MyDbContext db, LeagueDivision division, Models.LeagueScoringMode scoring)
    {
        var stats = await PlayerStatsService.ComputeAsync(
            db, [division.CompetitionId], scoring);

        return division.Competition.Participants
            .Where(p => p.Player != null)
            .Select(p =>
            {
                stats.TryGetValue(p.PlayerId, out var s);
                int played = s?.RubbersPlayed ?? 0;
                double winRate = played > 0 ? (s!.RubbersWon / (double)played) : 0.0;
                return new PlayerStatsDto(
                    p.PlayerId,
                    p.Player!.DisplayName,
                    division.Rank,
                    division.Name,
                    played,
                    s?.RubbersWon ?? 0,
                    s?.RubbersLost ?? 0,
                    s?.SetsWon ?? 0,
                    s?.SetsAgainst ?? 0,
                    s?.GamesWon ?? 0,
                    s?.GamesAgainst ?? 0,
                    s?.LeaguePoints ?? 0,
                    winRate);
            })
            .OrderByDescending(r => r.LeaguePoints)
            .ThenByDescending(r => r.RubbersWon)
            .ThenByDescending(r => r.SetsWon - r.SetsAgainst)
            .ThenByDescending(r => r.GamesWon - r.GamesAgainst)
            .ToList();
    }

    // ── Promotion / close ─────────────────────────────────────────────────

    [HttpGet("seasons/{id:int}/promotion-preview")]
    public async Task<ActionResult<PromotionPreviewDto>> PromotionPreview(int id)
    {
        await using var db = dbFactory.CreateDbContext();
        var season = await db.LeagueSeasons
            .Include(s => s.League)
            .Include(s => s.Divisions).ThenInclude(d => d.Competition).ThenInclude(c => c.Participants).ThenInclude(p => p.Player)
            .FirstOrDefaultAsync(s => s.LeagueSeasonId == id);
        if (season is null) return NotFound();

        var divs = new List<PromotionDivisionDto>();
        foreach (var d in season.Divisions.OrderBy(d => d.Rank))
        {
            var stats = await BuildStatsForDivision(db, d, season.League.LeagueScoring);
            var ranked = stats.Select((s, i) => new PromotionCandidateDto(
                s.PlayerId, s.DisplayName, d.Rank, i + 1, s.RubbersPlayed, s.LeaguePoints, s.WinRate))
                .ToList();
            divs.Add(new PromotionDivisionDto(d.Rank, d.Name, ranked));
        }
        return new PromotionPreviewDto(id, divs);
    }

    [HttpPost("seasons/{id:int}/close")]
    public async Task<ActionResult<CloseSeasonResultDto>> CloseSeason(int id, [FromBody] CloseSeasonRequest req)
    {
        await using var db = dbFactory.CreateDbContext();
        var season = await db.LeagueSeasons
            .Include(s => s.League).ThenInclude(l => l.Memberships)
            .FirstOrDefaultAsync(s => s.LeagueSeasonId == id);
        if (season is null) return NotFound();
        if (!await authzService.CanEditCompetitionAsync(User, season.League.HostClubId)) return Forbid();
        if (season.Status == LeagueSeasonStatus.Closed)
            return BadRequest(new { message = "Season is already closed." });

        int applied = 0;
        foreach (var decision in req.Decisions)
        {
            var membership = season.League.Memberships.FirstOrDefault(m => m.PlayerId == decision.PlayerId);
            if (membership is null) continue;
            membership.CurrentDivisionRank = decision.NewRank;
            applied++;
        }

        season.Status = LeagueSeasonStatus.Closed;
        season.ClosedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return new CloseSeasonResultDto(id, applied);
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private static LeagueDetailDto ToDetailDto(League l) => new(
        l.LeagueId,
        l.Name,
        l.HostClubId,
        l.HostClub?.Name,
        l.IsArchived,
        (Shared.CompetitionFormat)l.CompetitionFormat,
        l.TeamSize,
        (Shared.LeagueScoringMode)l.LeagueScoring,
        l.RubberTemplateKey,
        (Shared.MatchFormatType)l.MatchFormat,
        l.NumberOfSets,
        l.GamesPerSet,
        (Shared.SetWinMode)l.SetWinMode,
        l.TeamsPerDivisionTarget,
        l.TeamsPerDivisionMin);
}
