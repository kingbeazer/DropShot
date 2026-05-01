using DropShot.Data;
using DropShot.Models;
using DropShot.Shared.Dtos;
using DropShot.UI.Services;
using DropShot.UI.Services.Auth;
using Microsoft.EntityFrameworkCore;

namespace DropShot.Services;

/// <summary>
/// Web implementation of <see cref="IPlayerService"/>. Mirrors
/// <c>PlayersController</c> EF queries 1:1 so behaviour is identical between
/// the API and the new in-process abstraction.
/// </summary>
public sealed class WebPlayerService(
    IDbContextFactory<MyDbContext> dbFactory,
    ICurrentUser currentUser) : IPlayerService
{
    public async Task<List<PlayerDto>> GetPlayersAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var players = await db.Players
            .OrderBy(p => p.DisplayName)
            .ToListAsync(ct);
        return players.Select(ToDto).ToList();
    }

    public async Task<PlayerDto?> GetPlayerAsync(int id, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var p = await db.Players.FindAsync([id], ct);
        return p is null ? null : ToDto(p);
    }

    public async Task<List<GlobalLeagueTableEntryDto>> GetGlobalLeagueTableAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var matches = await db.SavedMatch
            .Where(m => m.Complete && m.Player1 != null)
            .Select(m => new { m.Player1, m.Player2, m.Player1Id, m.Player2Id, m.WinnerName, m.WinnerPlayerId })
            .ToListAsync(ct);

        var allPlayerIds = matches
            .SelectMany(m => new[] { m.Player1Id, m.Player2Id, m.WinnerPlayerId })
            .Where(id => id.HasValue).Select(id => id!.Value)
            .Distinct().ToList();
        var playerNames = await db.Players
            .Where(p => allPlayerIds.Contains(p.PlayerId))
            .ToDictionaryAsync(p => p.PlayerId, p => p.DisplayName, ct);

        string Resolve(string? name, int? id) =>
            id.HasValue && playerNames.TryGetValue(id.Value, out var n) ? n : name ?? "";

        var stats = new Dictionary<string, (int Played, int Won)>(StringComparer.OrdinalIgnoreCase);

        foreach (var m in matches)
        {
            var players = new[] { Resolve(m.Player1, m.Player1Id), Resolve(m.Player2, m.Player2Id) }
                .Where(p => !string.IsNullOrEmpty(p));

            foreach (var p in players)
            {
                if (!stats.ContainsKey(p)) stats[p] = (0, 0);
                stats[p] = (stats[p].Played + 1, stats[p].Won);
            }

            var winner = Resolve(m.WinnerName, m.WinnerPlayerId);
            if (!string.IsNullOrEmpty(winner) && stats.ContainsKey(winner))
                stats[winner] = (stats[winner].Played, stats[winner].Won + 1);
        }

        return stats
            .OrderByDescending(kv => kv.Value.Won)
            .Select(kv => new GlobalLeagueTableEntryDto(
                kv.Key,
                kv.Value.Played,
                kv.Value.Won,
                kv.Value.Played - kv.Value.Won,
                kv.Value.Won * 3))
            .ToList();
    }

    public async Task<List<PlayerWithClubsDto>> GetPlayersWithClubsAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var players = await db.Players
            .Include(p => p.User)
            .Include(p => p.ClubMemberships).ThenInclude(cp => cp.Club)
            .OrderBy(p => p.DisplayName)
            .ToListAsync(ct);

        return players.Select(p => new PlayerWithClubsDto(
            p.PlayerId, p.DisplayName, p.FirstName, p.LastName, p.Email,
            p.MobileNumber, p.DateOfBirth, (DropShot.Shared.PlayerSex?)p.Sex,
            p.ContactPreferences, p.ProfileImagePath, p.UserId, p.User?.UserName,
            p.IsLight, p.CreatedByUserId,
            p.ClubMemberships
                .Where(cp => cp.Club != null)
                .Select(cp => cp.Club.Name)
                .OrderBy(n => n)
                .ToList())).ToList();
    }

    public async Task<PlayerDto> CreatePlayerAsync(CreatePlayerRequest request, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var player = new Player
        {
            DisplayName = request.DisplayName.Trim(),
            FirstName = NullIfEmpty(request.FirstName),
            LastName = NullIfEmpty(request.LastName),
            Email = NullIfEmpty(request.Email),
            MobileNumber = NullIfEmpty(request.MobileNumber),
            DateOfBirth = request.DateOfBirth,
            Sex = (DropShot.Models.PlayerSex?)request.Sex,
            ContactPreferences = NullIfEmpty(request.ContactPreferences),
            IsLight = request.IsLight,
            CreatedByUserId = currentUser.UserId
        };
        db.Players.Add(player);
        await db.SaveChangesAsync(ct);
        return ToDto(player);
    }

    public async Task<PlayerDto> UpdatePlayerAsync(int playerId, UpdatePlayerRequest request, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var p = await db.Players.FindAsync([playerId], ct)
            ?? throw new KeyNotFoundException($"Player {playerId} not found.");

        p.DisplayName = request.DisplayName.Trim();
        p.FirstName = NullIfEmpty(request.FirstName);
        p.LastName = NullIfEmpty(request.LastName);
        p.Email = NullIfEmpty(request.Email);
        p.MobileNumber = NullIfEmpty(request.MobileNumber);
        p.DateOfBirth = request.DateOfBirth;
        p.Sex = (DropShot.Models.PlayerSex?)request.Sex;
        p.ContactPreferences = NullIfEmpty(request.ContactPreferences);
        await db.SaveChangesAsync(ct);
        return ToDto(p);
    }

    public async Task DeletePlayerAsync(int playerId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var p = await db.Players.FindAsync([playerId], ct);
        if (p is null) return;
        db.Players.Remove(p);
        await db.SaveChangesAsync(ct);
    }

    public async Task<List<ClubPlayerDto>> GetClubPlayersAsync(int clubId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var rows = await db.ClubPlayers
            .Where(cp => cp.ClubId == clubId)
            .Join(db.Players, cp => cp.PlayerId, p => p.PlayerId,
                (cp, p) => new
                {
                    p.PlayerId, p.DisplayName, p.FirstName, p.LastName,
                    p.IsLight, p.CreatedByClubId, p.Email, p.MobileNumber,
                    p.Sex, p.ProfileImagePath, p.DateOfBirth,
                    UserImage = p.User != null ? p.User.ProfileImagePath : null
                })
            .OrderBy(x => x.DisplayName)
            .ToListAsync(ct);

        return rows.Select(x => new ClubPlayerDto(
            x.PlayerId, x.DisplayName, x.FirstName, x.LastName,
            x.Email, x.MobileNumber, (DropShot.Shared.PlayerSex?)x.Sex,
            x.DateOfBirth, x.UserImage ?? x.ProfileImagePath,
            x.IsLight, x.CreatedByClubId == clubId)).ToList();
    }

    public async Task<List<PlayerDto>> SearchPlayersForClubLinkAsync(int clubId, string term, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(term)) return [];

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var alreadyInClub = await db.ClubPlayers
            .Where(cp => cp.ClubId == clubId)
            .Select(cp => cp.PlayerId)
            .ToListAsync(ct);

        var matches = await db.Players
            .Where(p => !alreadyInClub.Contains(p.PlayerId)
                && !p.IsLight
                && p.DisplayName.Contains(term))
            .OrderBy(p => p.DisplayName)
            .Take(10)
            .ToListAsync(ct);

        return matches.Select(ToDto).ToList();
    }

    public async Task<PlayerDto> CreateLightPlayerAsync(int clubId, CreateLightPlayerRequest request, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var player = new Player
        {
            DisplayName = request.DisplayName.Trim(),
            FirstName = NullIfEmpty(request.FirstName),
            LastName = NullIfEmpty(request.LastName),
            Email = NullIfEmpty(request.Email),
            MobileNumber = NullIfEmpty(request.MobileNumber),
            DateOfBirth = request.DateOfBirth,
            Sex = (DropShot.Models.PlayerSex?)request.Sex,
            IsLight = true,
            CreatedByClubId = clubId,
            CreatedByUserId = currentUser.UserId
        };
        db.Players.Add(player);
        await db.SaveChangesAsync(ct);

        db.ClubPlayers.Add(new ClubPlayer { ClubId = clubId, PlayerId = player.PlayerId });
        await db.SaveChangesAsync(ct);

        return ToDto(player);
    }

    public async Task<PlayerDto> UpdateLightPlayerAsync(int clubId, int playerId, UpdateLightPlayerRequest request, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var p = await db.Players.FindAsync([playerId], ct)
            ?? throw new KeyNotFoundException($"Player {playerId} not found.");

        if (!p.IsLight || p.CreatedByClubId != clubId)
            throw new InvalidOperationException("Only light players owned by this club can be edited.");

        p.DisplayName = request.DisplayName.Trim();
        p.FirstName = NullIfEmpty(request.FirstName);
        p.LastName = NullIfEmpty(request.LastName);
        p.Email = NullIfEmpty(request.Email);
        p.MobileNumber = NullIfEmpty(request.MobileNumber);
        p.DateOfBirth = request.DateOfBirth;
        p.Sex = (DropShot.Models.PlayerSex?)request.Sex;
        await db.SaveChangesAsync(ct);
        return ToDto(p);
    }

    public async Task RemovePlayerFromClubAsync(int clubId, int playerId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var cp = await db.ClubPlayers.FindAsync([clubId, playerId], ct);
        if (cp is null) return;

        db.ClubPlayers.Remove(cp);

        var player = await db.Players.FindAsync([playerId], ct);
        if (player is { IsLight: true } && player.CreatedByClubId == clubId)
        {
            db.Players.Remove(player);
        }

        await db.SaveChangesAsync(ct);
    }

    public async Task LinkExistingPlayerToClubAsync(int clubId, int playerId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var exists = await db.ClubPlayers.AnyAsync(cp => cp.ClubId == clubId && cp.PlayerId == playerId, ct);
        if (exists) return;
        db.ClubPlayers.Add(new ClubPlayer { ClubId = clubId, PlayerId = playerId });
        await db.SaveChangesAsync(ct);
    }

    private static string? NullIfEmpty(string? s) =>
        string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static PlayerDto ToDto(Player p) => new(
        p.PlayerId, p.DisplayName, p.FirstName, p.LastName, p.Email,
        p.DateOfBirth, (DropShot.Shared.PlayerSex?)p.Sex,
        p.ContactPreferences, p.ProfileImagePath, p.UserId, p.MobileNumber,
        p.IsLight, p.CreatedByUserId);
}
