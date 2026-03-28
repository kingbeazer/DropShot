using DropShot.Data;
using DropShot.Models;
using DropShot.Services;
using DropShot.Shared.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DropShot.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(AuthenticationSchemes = "Bearer")]
public class ClubsController(
    IDbContextFactory<MyDbContext> dbFactory,
    ClubAuthorizationService authzService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<ClubDto>>> GetAll()
    {
        await using var db = dbFactory.CreateDbContext();
        var clubs = await db.Clubs.Include(c => c.Courts).OrderBy(c => c.Name).ToListAsync();
        return clubs.Select(c => new ClubDto(
            c.ClubId, c.Name, c.AddressLine1, c.AddressLine2,
            c.Town, c.Postcode, c.Phone, c.Email, c.Website,
            c.Courts.Count)).ToList();
    }

    [HttpGet("{id:int}")]
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
}
