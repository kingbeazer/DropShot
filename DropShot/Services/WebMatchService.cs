using DropShot.Data;
using DropShot.Shared.Dtos;
using DropShot.UI.Services;
using DropShot.UI.Services.Auth;
using Microsoft.EntityFrameworkCore;

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
}
