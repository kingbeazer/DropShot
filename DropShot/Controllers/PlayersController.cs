using DropShot.Data;
using DropShot.Models;
using DropShot.Shared.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DropShot.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(AuthenticationSchemes = "Bearer")]
public class PlayersController(IDbContextFactory<MyDbContext> dbFactory) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<PlayerDto>>> GetAll()
    {
        await using var db = dbFactory.CreateDbContext();
        var players = await db.Players.OrderBy(p => p.DisplayName).ToListAsync();
        return players.Select(ToDto).ToList();
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
        await using var db = dbFactory.CreateDbContext();
        var player = new Player
        {
            DisplayName = req.DisplayName.Trim(),
            FirstName = req.FirstName,
            LastName = req.LastName,
            Email = req.Email,
            DateOfBirth = req.DateOfBirth,
            Sex = (DropShot.Models.PlayerSex?)req.Sex,
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
        await using var db = dbFactory.CreateDbContext();
        var p = await db.Players.FindAsync(id);
        if (p is null) return NotFound();

        p.DisplayName = req.DisplayName.Trim();
        p.FirstName = req.FirstName;
        p.LastName = req.LastName;
        p.Email = req.Email;
        p.DateOfBirth = req.DateOfBirth;
        p.Sex = (DropShot.Models.PlayerSex?)req.Sex;
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

    private static PlayerDto ToDto(Player p) => new(
        p.PlayerId, p.DisplayName, p.FirstName, p.LastName, p.Email,
        p.DateOfBirth, (DropShot.Shared.PlayerSex?)p.Sex,
        p.ContactPreferences, p.ProfileImagePath, p.UserId, p.MobileNumber);
}
