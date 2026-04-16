using System.Security.Claims;
using DropShot.Data;
using DropShot.Models;
using DropShot.Services;
using DropShot.Shared.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DropShot.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(AuthenticationSchemes = "Bearer")]
public class ClubsController(
    IDbContextFactory<MyDbContext> dbFactory,
    ClubAuthorizationService authzService,
    UserManager<ApplicationUser> userManager,
    AdminEmailService adminEmailService) : ControllerBase
{
    // ── Clubs directory ───────────────────────────────────────────────────────

    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<List<ClubDto>>> GetAll([FromQuery] int skip = 0, [FromQuery] int take = 50)
    {
        await using var db = dbFactory.CreateDbContext();
        var clubs = await db.Clubs.Include(c => c.Courts)
            .OrderBy(c => c.Name)
            .Skip(skip)
            .Take(Math.Min(take, 200))
            .ToListAsync();
        return clubs.Select(c => new ClubDto(
            c.ClubId, c.Name, c.AddressLine1, c.AddressLine2,
            c.Town, c.Postcode, c.Phone, c.Email, c.Website,
            c.Courts.Count)).ToList();
    }

    /// <summary>
    /// Returns the clubs directory augmented with the caller's link status per
    /// club. Requires authentication because <see cref="ClubLinkStatus"/> is
    /// caller-relative.
    /// </summary>
    [HttpGet("directory")]
    public async Task<ActionResult<List<ClubWithLinkStatusDto>>> GetDirectory(
        [FromQuery] int skip = 0, [FromQuery] int take = 50)
    {
        var userId = userManager.GetUserId(User);
        if (userId is null) return Unauthorized();

        await using var db = dbFactory.CreateDbContext();
        var clubs = await db.Clubs.Include(c => c.Courts)
            .OrderBy(c => c.Name)
            .Skip(skip)
            .Take(Math.Min(take, 200))
            .ToListAsync();

        var adminClubIds = (await db.ClubAdministrators
            .Where(ca => ca.UserId == userId)
            .Select(ca => ca.ClubId).ToListAsync()).ToHashSet();

        var player = await db.Players
            .Where(p => p.UserId == userId && !p.IsLight)
            .Select(p => (int?)p.PlayerId).FirstOrDefaultAsync();

        var linkedClubIds = player is int pid
            ? (await db.ClubPlayers.Where(cp => cp.PlayerId == pid && cp.IsActive)
                .Select(cp => cp.ClubId).ToListAsync()).ToHashSet()
            : new HashSet<int>();

        var pendingClubIds = (await db.ClubLinkRequests
            .Where(r => r.UserId == userId && r.Status == ClubLinkRequestStatus.Pending)
            .Select(r => r.ClubId).ToListAsync()).ToHashSet();

        return clubs.Select(c =>
        {
            var status = adminClubIds.Contains(c.ClubId) ? ClubLinkStatus.Administered
                : linkedClubIds.Contains(c.ClubId) ? ClubLinkStatus.Linked
                : pendingClubIds.Contains(c.ClubId) ? ClubLinkStatus.Pending
                : ClubLinkStatus.None;

            return new ClubWithLinkStatusDto(
                c.ClubId, c.Name, c.AddressLine1, c.AddressLine2,
                c.Town, c.Postcode, c.Phone, c.Email, c.Website,
                c.Courts.Count, status);
        }).ToList();
    }

    [HttpGet("{id:int}")]
    [AllowAnonymous]
    public async Task<ActionResult<ClubDetailDto>> Get(int id)
    {
        await using var db = dbFactory.CreateDbContext();
        var c = await db.Clubs.Include(x => x.Courts).FirstOrDefaultAsync(x => x.ClubId == id);
        if (c is null) return NotFound();

        return new ClubDetailDto(
            c.ClubId, c.Name, c.AddressLine1, c.AddressLine2,
            c.Town, c.Postcode, c.Phone, c.Email, c.Website,
            c.Courts.Select(ct => new CourtDto(ct.CourtId, ct.ClubId, ct.Name,
                (DropShot.Shared.CourtSurface)ct.Surface, ct.IsIndoor)).ToList());
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<ClubDto>> Create([FromBody] SaveClubRequest req)
    {
        await using var db = dbFactory.CreateDbContext();
        var club = Apply(new Club(), req);
        db.Clubs.Add(club);
        await db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = club.ClubId },
            new ClubDto(club.ClubId, club.Name, club.AddressLine1, club.AddressLine2,
                club.Town, club.Postcode, club.Phone, club.Email, club.Website, 0));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<ClubDto>> Update(int id, [FromBody] SaveClubRequest req)
    {
        if (!await authzService.CanEditClubAsync(User, id)) return Forbid();

        await using var db = dbFactory.CreateDbContext();
        var club = await db.Clubs.FindAsync(id);
        if (club is null) return NotFound();

        Apply(club, req);
        await db.SaveChangesAsync();
        return new ClubDto(club.ClubId, club.Name, club.AddressLine1, club.AddressLine2,
            club.Town, club.Postcode, club.Phone, club.Email, club.Website, 0);
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id)
    {
        await using var db = dbFactory.CreateDbContext();
        var club = await db.Clubs.FindAsync(id);
        if (club is null) return NotFound();
        db.Clubs.Remove(club);
        await db.SaveChangesAsync();
        return NoContent();
    }

    // ── Courts ────────────────────────────────────────────────────────────────

    [HttpPost("{id:int}/courts")]
    public async Task<ActionResult<CourtDto>> AddCourt(int id, [FromBody] AddCourtRequest req)
    {
        if (!await authzService.CanEditClubAsync(User, id)) return Forbid();

        await using var db = dbFactory.CreateDbContext();
        var court = new Court
        {
            ClubId = id, Name = req.Name.Trim(),
            Surface = (DropShot.Models.CourtSurface)req.Surface, IsIndoor = req.IsIndoor
        };
        db.Courts.Add(court);
        await db.SaveChangesAsync();
        return Ok(new CourtDto(court.CourtId, court.ClubId, court.Name,
            (DropShot.Shared.CourtSurface)court.Surface, court.IsIndoor));
    }

    [HttpPut("{id:int}/courts/{courtId:int}")]
    public async Task<ActionResult<CourtDto>> UpdateCourt(int id, int courtId, [FromBody] UpdateCourtRequest req)
    {
        if (!await authzService.CanEditClubAsync(User, id)) return Forbid();

        await using var db = dbFactory.CreateDbContext();
        var court = await db.Courts.FindAsync(courtId);
        if (court is null || court.ClubId != id) return NotFound();
        court.Name = req.Name.Trim();
        court.Surface = (DropShot.Models.CourtSurface)req.Surface;
        court.IsIndoor = req.IsIndoor;
        await db.SaveChangesAsync();
        return Ok(new CourtDto(court.CourtId, court.ClubId, court.Name,
            (DropShot.Shared.CourtSurface)court.Surface, court.IsIndoor));
    }

    [HttpDelete("{id:int}/courts/{courtId:int}")]
    public async Task<IActionResult> DeleteCourt(int id, int courtId)
    {
        if (!await authzService.CanEditClubAsync(User, id)) return Forbid();

        await using var db = dbFactory.CreateDbContext();
        var court = await db.Courts.FindAsync(courtId);
        if (court is null || court.ClubId != id) return NotFound();
        db.Courts.Remove(court);
        await db.SaveChangesAsync();
        return NoContent();
    }

    // ── Link requests ─────────────────────────────────────────────────────────

    /// <summary>User creates a new link request to a club.</summary>
    [HttpPost("{id:int}/link-requests")]
    public async Task<ActionResult<ClubLinkRequestDto>> CreateLinkRequest(int id)
    {
        var userId = userManager.GetUserId(User);
        if (userId is null) return Unauthorized();

        await using var db = dbFactory.CreateDbContext();

        var club = await db.Clubs.FindAsync(id);
        if (club is null) return NotFound();

        // Already linked?
        var player = await db.Players
            .Where(p => p.UserId == userId && !p.IsLight)
            .Select(p => (int?)p.PlayerId).FirstOrDefaultAsync();
        if (player is int pid && await db.ClubPlayers.AnyAsync(cp => cp.PlayerId == pid && cp.ClubId == id && cp.IsActive))
            return Conflict(new { message = "You are already linked to this club." });

        // Already pending?
        var existing = await db.ClubLinkRequests
            .FirstOrDefaultAsync(r => r.UserId == userId && r.ClubId == id && r.Status == ClubLinkRequestStatus.Pending);
        if (existing is not null)
            return Conflict(new { message = "You already have a pending request for this club." });

        var request = new ClubLinkRequest
        {
            ClubId = id,
            UserId = userId,
            Status = ClubLinkRequestStatus.Pending,
            RequestedAt = DateTime.UtcNow
        };
        db.ClubLinkRequests.Add(request);
        await db.SaveChangesAsync();

        // Email club admins (fire-and-log; don't block on it)
        var user = await userManager.FindByIdAsync(userId);
        if (user is not null)
        {
            var admins = await db.ClubAdministrators
                .Where(ca => ca.ClubId == id)
                .Select(ca => ca.User)
                .ToListAsync();
            await adminEmailService.SendClubLinkRequestReceivedAsync(club, user, admins);
        }

        return CreatedAtAction(nameof(GetLinkRequests), new { id },
            ToDto(request, club, user));
    }

    /// <summary>Club admin views pending link requests for their club.</summary>
    [HttpGet("{id:int}/link-requests")]
    public async Task<ActionResult<List<ClubLinkRequestDto>>> GetLinkRequests(
        int id, [FromQuery] ClubLinkRequestStatus? status = ClubLinkRequestStatus.Pending)
    {
        if (!await authzService.CanEditClubAsync(User, id)) return Forbid();

        await using var db = dbFactory.CreateDbContext();
        var q = db.ClubLinkRequests
            .Include(r => r.Club)
            .Include(r => r.User)
            .Where(r => r.ClubId == id);

        if (status.HasValue)
            q = q.Where(r => r.Status == status.Value);

        var rows = await q.OrderByDescending(r => r.RequestedAt).ToListAsync();
        return rows.Select(r => ToDto(r, r.Club, r.User)).ToList();
    }

    [HttpPost("{id:int}/link-requests/{requestId:int}/approve")]
    public async Task<IActionResult> ApproveLinkRequest(int id, int requestId)
    {
        if (!await authzService.CanEditClubAsync(User, id)) return Forbid();

        var resolverId = userManager.GetUserId(User);
        await using var db = dbFactory.CreateDbContext();

        var request = await db.ClubLinkRequests
            .Include(r => r.Club)
            .Include(r => r.User)
            .FirstOrDefaultAsync(r => r.ClubLinkRequestId == requestId && r.ClubId == id);
        if (request is null) return NotFound();
        if (request.Status != ClubLinkRequestStatus.Pending)
            return BadRequest(new { message = "This request has already been resolved." });

        // Ensure the requester has a Player; create one if missing.
        var player = await db.Players.FirstOrDefaultAsync(p => p.UserId == request.UserId && !p.IsLight);
        if (player is null)
        {
            var displayName = request.User.DisplayName is { Length: > 0 }
                ? request.User.DisplayName
                : request.User.UserName ?? "Player";
            player = new Player
            {
                DisplayName = displayName,
                Email = request.User.Email,
                UserId = request.UserId,
                IsLight = false,
                CreatedAt = DateTime.UtcNow
            };
            db.Players.Add(player);
            await db.SaveChangesAsync();
        }

        // Insert ClubPlayer if absent (idempotent).
        var alreadyLinked = await db.ClubPlayers.AnyAsync(cp => cp.ClubId == id && cp.PlayerId == player.PlayerId);
        if (!alreadyLinked)
        {
            db.ClubPlayers.Add(new ClubPlayer
            {
                ClubId = id,
                PlayerId = player.PlayerId,
                JoinedAt = DateTime.UtcNow,
                IsActive = true
            });
        }

        request.Status = ClubLinkRequestStatus.Approved;
        request.ResolvedAt = DateTime.UtcNow;
        request.ResolvedByUserId = resolverId;
        await db.SaveChangesAsync();

        await adminEmailService.SendClubLinkRequestApprovedAsync(request.Club, request.User);
        return NoContent();
    }

    [HttpPost("{id:int}/link-requests/{requestId:int}/reject")]
    public async Task<IActionResult> RejectLinkRequest(int id, int requestId)
    {
        if (!await authzService.CanEditClubAsync(User, id)) return Forbid();

        var resolverId = userManager.GetUserId(User);
        await using var db = dbFactory.CreateDbContext();

        var request = await db.ClubLinkRequests
            .Include(r => r.Club)
            .Include(r => r.User)
            .FirstOrDefaultAsync(r => r.ClubLinkRequestId == requestId && r.ClubId == id);
        if (request is null) return NotFound();
        if (request.Status != ClubLinkRequestStatus.Pending)
            return BadRequest(new { message = "This request has already been resolved." });

        request.Status = ClubLinkRequestStatus.Rejected;
        request.ResolvedAt = DateTime.UtcNow;
        request.ResolvedByUserId = resolverId;
        await db.SaveChangesAsync();

        await adminEmailService.SendClubLinkRequestRejectedAsync(request.Club, request.User);
        return NoContent();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static Club Apply(Club c, SaveClubRequest r)
    {
        c.Name = r.Name.Trim();
        c.AddressLine1 = r.AddressLine1;
        c.AddressLine2 = r.AddressLine2;
        c.Town = r.Town;
        c.Postcode = r.Postcode;
        c.Phone = r.Phone;
        c.Email = r.Email;
        c.Website = r.Website;
        return c;
    }

    private static ClubLinkRequestDto ToDto(ClubLinkRequest r, Club? club, ApplicationUser? user) => new(
        r.ClubLinkRequestId, r.ClubId, club?.Name ?? "", r.UserId,
        user?.DisplayName ?? user?.UserName ?? "", user?.Email,
        r.Status.ToString(), r.RequestedAt, r.ResolvedAt);
}
