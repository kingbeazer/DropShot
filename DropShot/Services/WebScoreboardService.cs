using System.Security.Claims;
using DropShot.Data;
using DropShot.Models;
using DropShot.Shared;
using DropShot.Shared.Dtos;
using DropShot.UI.Services;
using DropShot.UI.Services.Auth;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace DropShot.Services;

/// <summary>
/// Web implementation of <see cref="IScoreboardService"/>. Read surface for
/// the moved Scoreboard.razor: lists admin-scoped courts, builds the live
/// state snapshot for a single court (parses the latest GameState out of
/// SavedMatch.MatchJson), and persists per-court display settings.
/// </summary>
public sealed class WebScoreboardService(
    IDbContextFactory<MyDbContext> dbFactory,
    ClubAuthorizationService authzService,
    IHttpContextAccessor httpContextAccessor,
    ICurrentUser currentUser) : IScoreboardService
{
    public async Task<List<ScoreboardCourtDto>> GetAdminCourtsAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var user = httpContextAccessor.HttpContext?.User ?? new ClaimsPrincipal();
        var isAdmin = await authzService.IsAdminAsync(user);

        var query = db.Courts.Include(c => c.Club).AsQueryable();
        if (!isAdmin)
        {
            var clubIds = currentUser.AdminClubIds.ToList();
            if (clubIds.Count == 0) return [];
            query = query.Where(c => clubIds.Contains(c.ClubId));
        }

        var rows = await query.OrderBy(c => c.Club.Name).ThenBy(c => c.Name).ToListAsync(ct);
        return rows.Select(c => new ScoreboardCourtDto(c.CourtId, c.Club.Name, c.Name)).ToList();
    }

    public async Task<ScoreboardCourtStateDto> GetCourtStateAsync(int courtId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var setting = await db.ScoreboardDisplaySettings
            .FirstOrDefaultAsync(s => s.CourtId == courtId, ct);

        var displayDto = new ScoreboardDisplaySettingDto(
            courtId,
            setting?.Layout ?? "default",
            setting?.Fullscreen ?? false,
            setting?.LiveStreamUrl,
            setting?.ShowLiveStream ?? false);

        var match = await db.SavedMatch
            .FirstOrDefaultAsync(m => m.CourtId == courtId && !m.Complete, ct);

        ScoreboardActiveMatchDto? activeMatch = match is null
            ? null
            : new(match.Player1, match.Player2, match.Player3, match.Player4, match.CreatedAt);

        ScoreboardFixtureDto? activeFixture = null;
        if (match is not null)
        {
            var fx = await db.CompetitionFixtures
                .Include(f => f.Competition)
                .FirstOrDefaultAsync(f => f.SavedMatchId == match.SavedMatchId, ct);
            if (fx is not null)
                activeFixture = new(fx.Competition?.CompetitionName, fx.FixtureLabel);
        }

        var currentScore = ParseLatestGameState(match?.MatchJson) ?? EmptyScore();

        return new ScoreboardCourtStateDto(currentScore, activeMatch, activeFixture, displayDto);
    }

    public async Task UpdateDisplaySettingAsync(int courtId, UpdateDisplaySettingRequest request, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var courtExists = await db.Courts.AnyAsync(c => c.CourtId == courtId, ct);
        if (!courtExists) return;

        var setting = await db.ScoreboardDisplaySettings.FirstOrDefaultAsync(s => s.CourtId == courtId, ct);
        if (setting is null)
        {
            setting = new ScoreboardDisplaySetting { CourtId = courtId };
            db.ScoreboardDisplaySettings.Add(setting);
        }
        if (request.Layout is not null) setting.Layout = request.Layout;
        if (request.Fullscreen is not null) setting.Fullscreen = request.Fullscreen.Value;
        if (request.LiveStreamUrl is not null) setting.LiveStreamUrl = request.LiveStreamUrl;
        if (request.ShowLiveStream is not null) setting.ShowLiveStream = request.ShowLiveStream.Value;
        await db.SaveChangesAsync(ct);
    }

    private static GameState? ParseLatestGameState(string? matchJson)
    {
        if (string.IsNullOrWhiteSpace(matchJson)) return null;
        try
        {
            var match = JsonConvert.DeserializeObject<DropShot.Models.Match>(matchJson);
            return match?.HistoryList?.LastOrDefault();
        }
        catch (JsonException) { return null; }
    }

    private static GameState EmptyScore() =>
        new(0, 0, 0, 0, 0, 0, false, 0, true, 0, 0, new List<SetScore>(), DateTime.Now, false, "", "");
}
