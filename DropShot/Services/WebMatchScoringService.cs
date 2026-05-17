using DropShot.Data;
using DropShot.Models;
using DropShot.Shared;
using DropShot.Shared.Dtos;
using DropShot.UI.Services;
using DropShot.UI.Services.Auth;
using Microsoft.EntityFrameworkCore;

namespace DropShot.Services;

/// <summary>
/// Web implementation of <see cref="IMatchScoringService"/>. Phase 7 prep PR
/// for the TennisScore.razor migration. Mirrors the inline EF queries the
/// page does today; no behaviour change in this PR — the page move (PR 7e)
/// rewires the page to call this service instead of <c>IDbContextFactory</c>.
/// </summary>
public sealed class WebMatchScoringService(
    IDbContextFactory<MyDbContext> dbFactory,
    ICurrentUser currentUser) : IMatchScoringService
{
    public async Task<TennisScoreBootstrapDto> GetBootstrapAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var userId = currentUser.UserId;

        bool? preferredGameScoring = null;
        int? myPlayerId = null;
        IReadOnlyList<int> friendIds = Array.Empty<int>();

        if (!string.IsNullOrEmpty(userId))
        {
            var me = await db.Players
                .AsNoTracking()
                .Where(p => p.UserId == userId)
                .Select(p => new { p.PlayerId, p.DefaultGameScoring })
                .FirstOrDefaultAsync(ct);
            if (me is not null)
            {
                preferredGameScoring = me.DefaultGameScoring;
                myPlayerId = me.PlayerId;

                var pid = me.PlayerId;
                friendIds = await db.PlayerFriends
                    .Where(pf => (pf.PlayerId == pid || pf.FriendPlayerId == pid)
                                 && pf.Status == FriendStatus.Accepted)
                    .Select(pf => pf.PlayerId == pid ? pf.FriendPlayerId : pf.PlayerId)
                    .ToListAsync(ct);
            }
        }

        var players = await db.Players
            .AsNoTracking()
            .Include(p => p.User)
            .OrderBy(p => p.DisplayName)
            .Select(p => new ScoringPlayerDto(
                p.PlayerId,
                p.DisplayName,
                p.User != null ? p.User.ProfileImagePath : null))
            .ToListAsync(ct);

        return new TennisScoreBootstrapDto(preferredGameScoring, myPlayerId, players, friendIds);
    }

    public async Task<SavedMatchResumeDto?> GetSavedMatchForResumeAsync(
        int savedMatchId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var match = await db.SavedMatch
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.SavedMatchId == savedMatchId && !m.Complete, ct);
        if (match is null || string.IsNullOrEmpty(match.MatchJson))
            return null;

        TennisScoreFixtureContextDto? linked = null;
        var linkedFx = await db.CompetitionFixtures
            .AsNoTracking()
            .Include(f => f.Competition)
            .Include(f => f.Stage)
            .Include(f => f.Player1)
            .Include(f => f.Player2)
            .Include(f => f.Player3)
            .Include(f => f.Player4)
            .FirstOrDefaultAsync(f => f.SavedMatchId == match.SavedMatchId, ct);
        if (linkedFx is not null) linked = ToFixtureContextDto(linkedFx);

        return new SavedMatchResumeDto(
            match.SavedMatchId,
            match.MatchJson,
            match.CourtId,
            match.Player1Id, match.Player2Id, match.Player3Id, match.Player4Id,
            match.CreatedAt,
            linked);
    }

    public async Task<TennisScoreFixtureContextDto?> GetFixtureContextAsync(
        int fixtureId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var fx = await db.CompetitionFixtures
            .AsNoTracking()
            .Include(f => f.Competition)
            .Include(f => f.Stage)
            .Include(f => f.Player1)
            .Include(f => f.Player2)
            .Include(f => f.Player3)
            .Include(f => f.Player4)
            .FirstOrDefaultAsync(f => f.CompetitionFixtureId == fixtureId, ct);
        return fx is null ? null : ToFixtureContextDto(fx);
    }

    public async Task<TennisScoreRubberContextDto?> GetRubberContextAsync(
        int rubberId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var rubber = await db.Rubbers
            .AsNoTracking()
            .Include(r => r.Fixture).ThenInclude(f => f.Competition)
            .Include(r => r.Fixture).ThenInclude(f => f.HomeTeam)
            .Include(r => r.Fixture).ThenInclude(f => f.AwayTeam)
            .Include(r => r.HomePlayer1)
            .Include(r => r.HomePlayer2)
            .Include(r => r.AwayPlayer1)
            .Include(r => r.AwayPlayer2)
            .FirstOrDefaultAsync(r => r.RubberId == rubberId, ct);
        if (rubber is null) return null;

        var comp = rubber.Fixture.Competition;
        return new TennisScoreRubberContextDto(
            rubber.RubberId,
            rubber.CompetitionFixtureId,
            rubber.Fixture.CompetitionId,
            rubber.HomePlayer1Id, rubber.HomePlayer1?.DisplayName,
            rubber.HomePlayer2Id, rubber.HomePlayer2?.DisplayName,
            rubber.AwayPlayer1Id, rubber.AwayPlayer1?.DisplayName,
            rubber.AwayPlayer2Id, rubber.AwayPlayer2?.DisplayName,
            rubber.Fixture.HomeTeam?.Name,
            rubber.Fixture.AwayTeam?.Name,
            comp?.CompetitionName,
            comp?.MatchFormat ?? MatchFormatType.BestOf,
            comp?.BestOf ?? 3,
            comp?.NumberOfSets ?? 3,
            comp?.GamesPerSet ?? 6,
            comp?.SetWinMode ?? SetWinMode.WinBy2);
    }

    public async Task<List<ScoringCourtDto>> GetAvailableCourtsAsync(
        int? selectedCourtId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.Courts
            .AsNoTracking()
            .Include(c => c.Club)
            .Where(c => c.CourtId == selectedCourtId
                        || !db.SavedMatch.Any(m => !m.Complete && m.CourtId == c.CourtId))
            .OrderBy(c => c.Club.Name).ThenBy(c => c.Name)
            .Select(c => new ScoringCourtDto(c.CourtId, c.Club.Name, c.Name))
            .ToListAsync(ct);
    }

    public async Task SavePreferredGameScoringAsync(bool gameScoring, CancellationToken ct = default)
    {
        var userId = currentUser.UserId;
        if (string.IsNullOrEmpty(userId)) return;

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var player = await db.Players.FirstOrDefaultAsync(p => p.UserId == userId, ct);
        if (player is null) return;
        player.DefaultGameScoring = gameScoring;
        await db.SaveChangesAsync(ct);
    }

    public async Task SendFriendRequestAsync(int targetPlayerId, CancellationToken ct = default)
    {
        var userId = currentUser.UserId;
        if (string.IsNullOrEmpty(userId)) return;

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var myPlayerId = await db.Players
            .Where(p => p.UserId == userId)
            .Select(p => (int?)p.PlayerId)
            .FirstOrDefaultAsync(ct);
        if (myPlayerId is null) return;
        if (myPlayerId.Value == targetPlayerId) return;

        var exists = await db.PlayerFriends.AnyAsync(pf =>
            (pf.PlayerId == myPlayerId.Value && pf.FriendPlayerId == targetPlayerId)
            || (pf.PlayerId == targetPlayerId && pf.FriendPlayerId == myPlayerId.Value), ct);
        if (exists) return;

        db.PlayerFriends.Add(new PlayerFriend
        {
            PlayerId = myPlayerId.Value,
            FriendPlayerId = targetPlayerId,
            Status = FriendStatus.Pending,
            RequestedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync(ct);
    }

    public async Task<int> UpsertLiveMatchAsync(
        UpsertLiveMatchRequest request, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var userId = currentUser.UserId;

        SavedMatch? match = null;
        if (request.SavedMatchId is int existingId && existingId > 0)
        {
            match = await db.SavedMatch.FirstOrDefaultAsync(m => m.SavedMatchId == existingId, ct);
            if (match is not null)
            {
                // Ownership check: caller must own the row by UserId or, for
                // anonymous rows, by DeviceToken.
                if (!string.IsNullOrEmpty(userId))
                {
                    if (match.UserId != userId)
                        throw new UnauthorizedAccessException("SavedMatch is not owned by the current user.");
                }
                else
                {
                    if (match.UserId is not null
                        || string.IsNullOrEmpty(request.DeviceToken)
                        || match.DeviceToken != request.DeviceToken)
                        throw new UnauthorizedAccessException("SavedMatch is not owned by this device.");
                }
            }
        }

        if (match is null)
        {
            match = new SavedMatch
            {
                CreatedAt = DateTime.UtcNow,
                UserId = userId,
                DeviceToken = string.IsNullOrEmpty(userId) ? request.DeviceToken : null
            };
            db.SavedMatch.Add(match);
        }

        match.MatchJson = request.MatchJson;
        bool wasComplete = match.Complete;
        match.Complete = request.Complete;
        if (match.Complete && !wasComplete)
            match.CompletedAt = DateTime.UtcNow;
        else if (!match.Complete)
            match.CompletedAt = null;
        match.LastActivityAt = DateTime.UtcNow;
        match.Player1 = request.Player1;
        match.Player2 = request.Player2;
        match.Player3 = request.Player3;
        match.Player4 = request.Player4;
        match.Player1Id = request.Player1Id;
        match.Player2Id = request.Player2Id;
        match.Player3Id = request.Player3Id;
        match.Player4Id = request.Player4Id;
        match.WinnerName = request.WinnerName;
        match.WinnerPlayerId = request.WinnerPlayerId;
        match.CourtId = request.CourtId;

        await db.SaveChangesAsync(ct);

        if (request.LinkedFixtureId is int fxId && fxId > 0)
        {
            var fx = await db.CompetitionFixtures.FindAsync(new object?[] { fxId }, ct);
            if (fx is not null && fx.SavedMatchId != match.SavedMatchId)
            {
                fx.SavedMatchId = match.SavedMatchId;
                if (fx.Status == FixtureStatus.Scheduled)
                    fx.Status = FixtureStatus.InProgress;
                await db.SaveChangesAsync(ct);
            }
        }

        return match.SavedMatchId;
    }

    public async Task FinaliseLiveFixtureAsync(
        int fixtureId, FinaliseLiveFixtureRequest request, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var fx = await db.CompetitionFixtures.FindAsync(new object?[] { fixtureId }, ct);
        if (fx is null || fx.Status == FixtureStatus.Completed) return;

        fx.Status = FixtureStatus.Completed;
        fx.CompletedAt = DateTime.UtcNow;
        fx.SavedMatchId = request.SavedMatchId;
        fx.WinnerPlayerId = request.WinnerPlayerId;
        fx.ResultSummary = request.ResultSummary;
        fx.HomeSetsWon = request.HomeSetsWon;
        fx.AwaySetsWon = request.AwaySetsWon;
        fx.HomeGamesTotal = request.HomeGamesTotal;
        fx.AwayGamesTotal = request.AwayGamesTotal;

        await db.SaveChangesAsync(ct);
        await LadderRatingService.ApplyForFinalisedFixtureAsync(db, fx.CompetitionFixtureId, ct);
        await CompetitionProgressionService.TryAdvanceAsync(db, fx.CompetitionId, fx.CompetitionFixtureId);
    }

    public async Task DiscardLiveMatchAsync(
        int savedMatchId, string? deviceToken, CancellationToken ct = default)
    {
        if (savedMatchId <= 0) return;

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var match = await db.SavedMatch.FirstOrDefaultAsync(m => m.SavedMatchId == savedMatchId, ct);
        if (match is null) return;

        var userId = currentUser.UserId;
        if (!string.IsNullOrEmpty(userId))
        {
            if (match.UserId != userId)
                throw new UnauthorizedAccessException("SavedMatch is not owned by the current user.");
        }
        else
        {
            if (match.UserId is not null
                || string.IsNullOrEmpty(deviceToken)
                || match.DeviceToken != deviceToken)
                throw new UnauthorizedAccessException("SavedMatch is not owned by this device.");
        }

        var linkedFx = await db.CompetitionFixtures
            .FirstOrDefaultAsync(f => f.SavedMatchId == savedMatchId, ct);
        if (linkedFx is not null)
        {
            linkedFx.Status = FixtureStatus.Scheduled;
            linkedFx.CompletedAt = null;
            linkedFx.SavedMatchId = null;
            linkedFx.WinnerPlayerId = null;
            linkedFx.WinnerTeamId = null;
            linkedFx.ResultSummary = null;
        }

        var linkedRubbers = await db.Rubbers
            .Where(r => r.SavedMatchId == savedMatchId)
            .ToListAsync(ct);
        foreach (var rub in linkedRubbers)
        {
            rub.IsComplete = false;
            rub.SavedMatchId = null;
            rub.HomeGames = null;
            rub.AwayGames = null;
            rub.WinnerTeamId = null;
        }

        db.SavedMatch.Remove(match);
        await db.SaveChangesAsync(ct);
    }

    public async Task<int> CreateLadderFixtureAsync(
        CreateLadderFixtureRequest request, CancellationToken ct = default)
    {
        var userId = currentUser.UserId;
        if (string.IsNullOrEmpty(userId))
            throw new UnauthorizedAccessException("Sign-in required to record a ladder match.");

        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var callerPlayerId = await db.Players
            .Where(p => p.UserId == userId)
            .Select(p => (int?)p.PlayerId)
            .FirstOrDefaultAsync(ct);
        if (callerPlayerId is null)
            throw new UnauthorizedAccessException("No Player profile linked to the current user.");
        if (callerPlayerId.Value == request.OpponentPlayerId)
            throw new InvalidOperationException("Cannot record a match against yourself.");

        var comp = await db.Competition
            .FirstOrDefaultAsync(c => c.CompetitionID == request.CompetitionId, ct);
        if (comp is null)
            throw new InvalidOperationException("Competition not found.");
        if (comp.CompetitionFormat != CompetitionFormat.SinglesLadder)
            throw new InvalidOperationException("Ad-hoc fixture creation is only valid for SinglesLadder competitions.");

        var enrolled = await db.CompetitionParticipants
            .Where(p => p.CompetitionId == request.CompetitionId
                        && p.Status == ParticipantStatus.FullPlayer
                        && (p.PlayerId == callerPlayerId.Value || p.PlayerId == request.OpponentPlayerId))
            .Select(p => p.PlayerId)
            .ToListAsync(ct);
        if (!enrolled.Contains(callerPlayerId.Value))
            throw new UnauthorizedAccessException("You are not an active participant in this ladder.");
        if (!enrolled.Contains(request.OpponentPlayerId))
            throw new InvalidOperationException("Opponent is not an active participant in this ladder.");

        var fx = new CompetitionFixture
        {
            CompetitionId = request.CompetitionId,
            Player1Id = callerPlayerId.Value,
            Player2Id = request.OpponentPlayerId,
            ScheduledAt = DateTime.UtcNow,
            Status = FixtureStatus.Scheduled,
        };
        db.CompetitionFixtures.Add(fx);
        await db.SaveChangesAsync(ct);
        return fx.CompetitionFixtureId;
    }

    private static TennisScoreFixtureContextDto ToFixtureContextDto(CompetitionFixture fx)
    {
        var comp = fx.Competition;
        return new TennisScoreFixtureContextDto(
            fx.CompetitionFixtureId,
            fx.CompetitionId,
            comp?.CompetitionName,
            fx.Stage?.Name,
            fx.FixtureLabel,
            fx.CourtId,
            fx.Player1Id, fx.Player1?.DisplayName,
            fx.Player2Id, fx.Player2?.DisplayName,
            fx.Player3Id, fx.Player3?.DisplayName,
            fx.Player4Id, fx.Player4?.DisplayName,
            fx.Status,
            comp?.MatchFormat ?? MatchFormatType.BestOf,
            comp?.BestOf ?? 3,
            comp?.NumberOfSets ?? 3,
            comp?.GamesPerSet ?? 6,
            comp?.SetWinMode ?? SetWinMode.WinBy2);
    }
}
