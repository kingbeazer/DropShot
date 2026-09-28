using DropShot.Data;
using DropShot.Models;
using DropShot.Shared.Dtos;
using DropShot.UI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DropShot.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(AuthenticationSchemes = "Bearer")]
public class PlayersController(
    IDbContextFactory<MyDbContext> dbFactory,
    IPlayerService playerService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<PlayerDto>>> GetAll([FromQuery] int skip = 0, [FromQuery] int take = 50)
    {
        await using var db = dbFactory.CreateDbContext();
        var players = await db.Players
            .OrderBy(p => p.DisplayName)
            .Skip(skip)
            .Take(Math.Min(take, 200))
            .ToListAsync();
        return players.Select(ToDto).ToList();
    }

    /// <summary>
    /// Global cross-club league table aggregated from <c>SavedMatch</c>.
    /// Backs the LeagueTable page on MAUI (phase 4).
    /// </summary>
    [HttpGet("league-table")]
    public async Task<ActionResult<List<GlobalLeagueTableEntryDto>>> GetLeagueTable(CancellationToken ct)
    {
        return await playerService.GetGlobalLeagueTableAsync(ct);
    }

    /// <summary>
    /// Players with their linked accounts and club memberships joined in.
    /// Backs the SuperAdmin Players page on MAUI (phase 4 batch B).
    /// </summary>
    [HttpGet("with-clubs")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<ActionResult<List<PlayerWithClubsDto>>> GetWithClubs(CancellationToken ct)
    {
        return await playerService.GetPlayersWithClubsAsync(ct);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<PlayerDto>> Get(int id)
    {
        await using var db = dbFactory.CreateDbContext();
        var p = await db.Players.FindAsync(id);
        return p is null ? NotFound() : ToDto(p);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<PlayerDto>> Create([FromBody] CreatePlayerRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.DisplayName))
            return BadRequest(new { message = "DisplayName is required." });
        if (req.DisplayName.Length > 100)
            return BadRequest(new { message = "DisplayName must be 100 characters or less." });
        if (req.Email != null && !req.Email.Contains('@'))
            return BadRequest(new { message = "Invalid email format." });

        await using var db = dbFactory.CreateDbContext();
        var player = new Player
        {
            DisplayName = req.DisplayName.Trim(),
            FirstName = req.FirstName,
            LastName = req.LastName,
            Email = req.Email,
            DateOfBirth = req.DateOfBirth,
            Sex = req.Sex,
            ContactPreferences = req.ContactPreferences,
            MobileNumber = req.MobileNumber
        };
        db.Players.Add(player);
        await db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = player.PlayerId }, ToDto(player));
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<PlayerDto>> Update(int id, [FromBody] UpdatePlayerRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.DisplayName))
            return BadRequest(new { message = "DisplayName is required." });
        if (req.DisplayName.Length > 100)
            return BadRequest(new { message = "DisplayName must be 100 characters or less." });
        if (req.Email != null && !req.Email.Contains('@'))
            return BadRequest(new { message = "Invalid email format." });

        await using var db = dbFactory.CreateDbContext();
        var p = await db.Players.FindAsync(id);
        if (p is null) return NotFound();

        p.DisplayName = req.DisplayName.Trim();
        p.FirstName = req.FirstName;
        p.LastName = req.LastName;
        p.Email = req.Email;
        p.DateOfBirth = req.DateOfBirth;
        p.Sex = req.Sex;
        p.ContactPreferences = req.ContactPreferences;
        p.MobileNumber = req.MobileNumber;
        await db.SaveChangesAsync();
        return ToDto(p);
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id)
    {
        await using var db = dbFactory.CreateDbContext();
        var p = await db.Players.FindAsync(id);
        if (p is null) return NotFound();
        db.Players.Remove(p);
        await db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>The current user's "my players" — light players they own + bookmarked verified players.</summary>
    [HttpGet("mine")]
    public async Task<ActionResult<List<MyPlayerRowDto>>> GetMine(CancellationToken ct)
    {
        return await playerService.GetMyPlayersAsync(ct);
    }

    [HttpPost("mine")]
    public async Task<ActionResult<PlayerDto>> CreateMine(
        [FromBody] CreateMyLightPlayerRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.DisplayName))
            return BadRequest(new { message = "DisplayName is required." });
        try
        {
            var dto = await playerService.CreateMyLightPlayerAsync(req, ct);
            return CreatedAtAction(nameof(Get), new { id = dto.PlayerId }, dto);
        }
        catch (InvalidOperationException) { return Forbid(); }
    }

    [HttpPut("mine/{id:int}")]
    public async Task<ActionResult<PlayerDto>> UpdateMine(
        int id, [FromBody] UpdateMyLightPlayerRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.DisplayName))
            return BadRequest(new { message = "DisplayName is required." });
        try
        {
            return await playerService.UpdateMyLightPlayerAsync(id, req, ct);
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (InvalidOperationException) { return Forbid(); }
    }

    [HttpDelete("mine/{id:int}")]
    public async Task<IActionResult> DeleteMine(int id, CancellationToken ct)
    {
        try
        {
            await playerService.DeleteMyLightPlayerAsync(id, ct);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpPost("mine/{lightId:int}/link-to/{verifiedId:int}")]
    public async Task<IActionResult> LinkLightToVerified(int lightId, int verifiedId, CancellationToken ct)
    {
        try
        {
            await playerService.LinkLightToVerifiedAsync(lightId, verifiedId, ct);
            return NoContent();
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (InvalidOperationException) { return Forbid(); }
    }

    [HttpGet("search-similar")]
    public async Task<ActionResult<List<SimilarPlayerDto>>> SearchSimilar(
        [FromQuery] string term, [FromQuery] int max = 5, CancellationToken ct = default)
    {
        return await playerService.SearchSimilarVerifiedPlayersAsync(term, max, ct);
    }

    /// <summary>
    /// Set or clear <c>Player.UserId</c>. SuperAdmin only — exposed only for
    /// the SuperAdmin Players page's "Link account" affordance.
    /// </summary>
    [HttpPut("{id:int}/account")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> LinkAccount(
        int id, [FromBody] LinkPlayerAccountRequest req, CancellationToken ct)
    {
        try
        {
            await playerService.LinkPlayerAccountAsync(id, req.UserId, ct);
            return NoContent();
        }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    private static PlayerDto ToDto(Player p) => new(
        p.PlayerId, p.DisplayName, p.FirstName, p.LastName, p.Email,
        p.DateOfBirth, (DropShot.Shared.PlayerSex?)p.Sex,
        p.ContactPreferences, p.ProfileImagePath, p.UserId, p.MobileNumber,
        p.IsLight, p.CreatedByUserId);
}

/// <summary>
/// Per-club player roster endpoints. Backs ClubPlayers.razor (phase 4 batch B.3).
/// All routes require an active ClubAdmin / Admin / SuperAdmin role with edit
/// rights on the club (delegated to <see cref="ClubAuthorizationService"/>).
/// </summary>
[ApiController]
[Route("api/clubs/{clubId:int}/players")]
[Authorize(AuthenticationSchemes = "Bearer", Roles = "ClubAdmin,Admin,SuperAdmin")]
public class ClubPlayersController(
    IPlayerService playerService,
    DropShot.Services.ClubAuthorizationService authzService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<ClubPlayerDto>>> GetClubPlayers(int clubId, CancellationToken ct)
    {
        if (!await authzService.CanEditClubAsync(User, clubId)) return Forbid();
        return await playerService.GetClubPlayersAsync(clubId, ct);
    }

    [HttpGet("search")]
    public async Task<ActionResult<List<PlayerDto>>> SearchForLink(
        int clubId, [FromQuery] string term, CancellationToken ct)
    {
        if (!await authzService.CanEditClubAsync(User, clubId)) return Forbid();
        return await playerService.SearchPlayersForClubLinkAsync(clubId, term, ct);
    }

    [HttpPost("light")]
    public async Task<ActionResult<PlayerDto>> CreateLight(
        int clubId, [FromBody] CreateLightPlayerRequest req, CancellationToken ct)
    {
        if (!await authzService.CanEditClubAsync(User, clubId)) return Forbid();
        if (string.IsNullOrWhiteSpace(req.DisplayName))
            return BadRequest(new { message = "DisplayName is required." });
        var dto = await playerService.CreateLightPlayerAsync(clubId, req, ct);
        return CreatedAtAction(nameof(GetClubPlayers), new { clubId }, dto);
    }

    [HttpPut("light/{playerId:int}")]
    public async Task<ActionResult<PlayerDto>> UpdateLight(
        int clubId, int playerId, [FromBody] UpdateLightPlayerRequest req, CancellationToken ct)
    {
        if (!await authzService.CanEditClubAsync(User, clubId)) return Forbid();
        if (string.IsNullOrWhiteSpace(req.DisplayName))
            return BadRequest(new { message = "DisplayName is required." });
        try
        {
            return await playerService.UpdateLightPlayerAsync(clubId, playerId, req, ct);
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (InvalidOperationException) { return Forbid(); }
    }

    [HttpPost("{playerId:int}/link")]
    public async Task<IActionResult> LinkExisting(int clubId, int playerId, CancellationToken ct)
    {
        if (!await authzService.CanEditClubAsync(User, clubId)) return Forbid();
        await playerService.LinkExistingPlayerToClubAsync(clubId, playerId, ct);
        return NoContent();
    }

    [HttpDelete("{playerId:int}")]
    public async Task<IActionResult> RemoveFromClub(int clubId, int playerId, CancellationToken ct)
    {
        if (!await authzService.CanEditClubAsync(User, clubId)) return Forbid();
        await playerService.RemovePlayerFromClubAsync(clubId, playerId, ct);
        return NoContent();
    }

    [HttpPut("{playerId:int}/archive")]
    public async Task<IActionResult> Archive(int clubId, int playerId, CancellationToken ct)
    {
        if (!await authzService.CanEditClubAsync(User, clubId)) return Forbid();
        await playerService.ArchivePlayerFromClubAsync(clubId, playerId, ct);
        return NoContent();
    }

    [HttpPut("{playerId:int}/unarchive")]
    public async Task<IActionResult> Unarchive(int clubId, int playerId, CancellationToken ct)
    {
        if (!await authzService.CanEditClubAsync(User, clubId)) return Forbid();
        await playerService.UnarchivePlayerFromClubAsync(clubId, playerId, ct);
        return NoContent();
    }

    [HttpGet("import-template")]
    public IActionResult DownloadImportTemplate()
    {
        var csv = "DisplayName,FirstName,LastName,Email,MobileNumber,DateOfBirth,Sex\r\n" +
                  "John Smith,John,Smith,john@example.com,07700900000,01/01/1990,Male\r\n";
        var bytes = System.Text.Encoding.UTF8.GetBytes(csv);
        return File(bytes, "text/csv", "player-import-template.csv");
    }
}
