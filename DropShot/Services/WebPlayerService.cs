using DropShot.Data;
using DropShot.Models;
using DropShot.Shared.Dtos;
using DropShot.UI.Services;
using Microsoft.EntityFrameworkCore;

namespace DropShot.Services;

/// <summary>
/// Web implementation of <see cref="IPlayerService"/>. Mirrors
/// <c>PlayersController</c> EF queries 1:1 so behaviour is identical between
/// the API and the new in-process abstraction. Phase 3 seed: read surface only.
/// </summary>
public sealed class WebPlayerService(IDbContextFactory<MyDbContext> dbFactory) : IPlayerService
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

    private static PlayerDto ToDto(Player p) => new(
        p.PlayerId, p.DisplayName, p.FirstName, p.LastName, p.Email,
        p.DateOfBirth, (DropShot.Shared.PlayerSex?)p.Sex,
        p.ContactPreferences, p.ProfileImagePath, p.UserId, p.MobileNumber,
        p.IsLight, p.CreatedByUserId);
}
