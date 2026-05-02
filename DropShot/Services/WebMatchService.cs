using DropShot.Data;
using DropShot.Models;
using DropShot.Shared;
using DropShot.Shared.Dtos;
using DropShot.UI.Services;
using DropShot.UI.Services.Auth;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace DropShot.Services;

/// <summary>
/// Web implementation of <see cref="IMatchService"/>. Phase 5: Match landing
/// page reads. TeamMatchScoring + scoring-write endpoints land in phase 6.
/// </summary>
public sealed class WebMatchService(
    IDbContextFactory<MyDbContext> dbFactory,
    ICurrentUser currentUser) : IMatchService
{
    public async Task<List<ActiveMatchDto>> GetMyActiveMatchesAsync(
        string? deviceToken, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var userId = currentUser.UserId;
        IQueryable<DropShot.Models.SavedMatch> q;
        if (!string.IsNullOrEmpty(userId))
        {
            q = db.SavedMatch.Where(m => !m.Complete && m.UserId == userId);
        }
        else if (!string.IsNullOrEmpty(deviceToken))
        {
            q = db.SavedMatch.Where(m => !m.Complete && m.DeviceToken == deviceToken && m.UserId == null);
        }
        else
        {
            return [];
        }

        var matches = await q.OrderByDescending(m => m.CreatedAt).ToListAsync(ct);
        if (matches.Count == 0) return [];

        var courtIds = matches.Where(m => m.CourtId.HasValue)
            .Select(m => m.CourtId!.Value).Distinct().ToList();
        var courtNames = courtIds.Count > 0
            ? await db.Courts.Include(c => c.Club)
                .Where(c => courtIds.Contains(c.CourtId))
                .ToDictionaryAsync(c => c.CourtId, c => $"{c.Club.Name} — {c.Name}", ct)
            : new Dictionary<int, string>();

        return matches.Select(m => new ActiveMatchDto(
            m.SavedMatchId, m.Player1, m.Player2, m.Player3, m.Player4,
            m.CourtId,
            m.CourtId.HasValue && courtNames.TryGetValue(m.CourtId.Value, out var n) ? n : null,
            m.CreatedAt)).ToList();
    }

    public async Task<List<RecentCasualMatchDto>> GetMyRecentCasualMatchesAsync(
        int limit = 6, CancellationToken ct = default)
    {
        var userId = currentUser.UserId;
        if (string.IsNullOrEmpty(userId)) return [];

        var safeLimit = Math.Clamp(limit, 1, 50);

        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var pid = await db.Players
            .Where(p => p.UserId == userId)
            .Select(p => (int?)p.PlayerId)
            .FirstOrDefaultAsync(ct);
        if (pid is null) return [];

        // SavedMatch rows linked to a fixture render as fixture results, not casual.
        var linkedSavedMatchIds = db.CompetitionFixtures
            .Where(f => f.SavedMatchId != null)
            .Select(f => f.SavedMatchId!.Value);

        var rows = await db.SavedMatch
            .Where(m => m.Complete
                     && (m.Player1Id == pid || m.Player2Id == pid
                         || m.Player3Id == pid || m.Player4Id == pid)
                     && !linkedSavedMatchIds.Contains(m.SavedMatchId))
            .OrderByDescending(m => m.CompletedAt ?? m.CreatedAt)
            .Take(safeLimit)
            .ToListAsync(ct);

        return rows.Select(m => new RecentCasualMatchDto(
            m.SavedMatchId,
            m.Player1, m.Player2, m.Player3, m.Player4,
            m.Player1Id, m.Player2Id, m.Player3Id, m.Player4Id,
            m.WinnerName, m.WinnerPlayerId,
            m.CreatedAt, m.CompletedAt,
            ParseSets(m.MatchJson))).ToList();
    }

    /// <summary>
    /// Pulls the latest GameState snapshot from MatchJson and emits per-set
    /// game counts. Falls back to a single (UserG, OppG) pseudo-set for
    /// game-only scored matches that never recorded a per-set breakdown.
    /// </summary>
    private static IReadOnlyList<CasualSetScoreDto> ParseSets(string? matchJson)
    {
        if (string.IsNullOrWhiteSpace(matchJson)) return Array.Empty<CasualSetScoreDto>();
        try
        {
            var match = JsonConvert.DeserializeObject<Match>(matchJson);
            // HistoryList is serialized from a Stack, so index 0 is the most
            // recent state. Fall back to History (the actual Stack) for older
            // records that don't have HistoryList populated.
            var latest = match?.HistoryList?.FirstOrDefault()
                ?? match?.History?.FirstOrDefault();
            if (latest is null) return Array.Empty<CasualSetScoreDto>();

            if (latest.SetScores is { Count: > 0 } setScores)
            {
                return setScores
                    .OrderBy(s => s.SetNumber)
                    .Select(s => new CasualSetScoreDto(s.SetNumber, s.UserGames, s.OpponentGames))
                    .ToList();
            }

            if (latest.UserG > 0 || latest.OppG > 0)
                return new[] { new CasualSetScoreDto(1, latest.UserG, latest.OppG) };

            return Array.Empty<CasualSetScoreDto>();
        }
        catch
        {
            return Array.Empty<CasualSetScoreDto>();
        }
    }
}
