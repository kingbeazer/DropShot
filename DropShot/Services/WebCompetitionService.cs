using DropShot.Data;
using DropShot.Models;
using DropShot.Shared;
using DropShot.Shared.Dtos;
using DropShot.UI.Services;
using DropShot.UI.Services.Auth;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace DropShot.Services;

/// <summary>
/// Web implementation of <see cref="ICompetitionService"/>. Mirrors
/// <c>CompetitionsController</c> read endpoints, including the
/// visibility filter via <see cref="ClubAuthorizationService"/>. Phase 3 seed:
/// read surface only.
/// </summary>
public sealed class WebCompetitionService(
    IDbContextFactory<MyDbContext> dbFactory,
    ClubAuthorizationService authzService,
    IHttpContextAccessor httpContextAccessor,
    ICurrentUser currentUser,
    BackgroundTaskQueue backgroundTasks,
    RubberResolutionService rubberResolver) : ICompetitionService
{
    public async Task<List<CompetitionDto>> GetCompetitionsAsync(bool includeArchived = false, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var baseQuery = db.Competition
            .Include(c => c.HostClub)
            .Include(c => c.Rules)
            .Include(c => c.Event)
            .AsQueryable();

        if (!includeArchived)
            baseQuery = baseQuery.Where(c => !c.IsArchived);

        var user = httpContextAccessor.HttpContext?.User ?? new ClaimsPrincipal();
        var visCtx = await authzService.GetVisibilityContextAsync(user);
        var query = authzService.ApplyVisibilityFilter(baseQuery, db, visCtx);

        var comps = await query
            .OrderBy(c => c.CompetitionName)
            .ToListAsync(ct);
        return comps.Select(ToDto).ToList();
    }

    public async Task<CompetitionDetailDto?> GetCompetitionAsync(int id, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var c = await db.Competition
            .AsSplitQuery()
            .AsNoTracking()
            .Include(x => x.HostClub)
            .Include(x => x.Rules)
            .Include(x => x.Event)
            .Include(x => x.Stages.OrderBy(s => s.StageOrder))
            .Include(x => x.Participants).ThenInclude(p => p.Player)
            .Include(x => x.Participants).ThenInclude(p => p.Team)
            .Include(x => x.Participants).ThenInclude(p => p.Division)
            .Include(x => x.Divisions)
            .Include(x => x.AllowedPlayers)
            .Include(x => x.Fixtures).ThenInclude(f => f.Stage)
            .Include(x => x.Fixtures).ThenInclude(f => f.Court)
            .Include(x => x.Fixtures).ThenInclude(f => f.CourtPair).ThenInclude(cp => cp!.Court1)
            .Include(x => x.Fixtures).ThenInclude(f => f.CourtPair).ThenInclude(cp => cp!.Court2)
            .Include(x => x.Fixtures).ThenInclude(f => f.Player1)
            .Include(x => x.Fixtures).ThenInclude(f => f.Player2)
            .Include(x => x.Fixtures).ThenInclude(f => f.Player3)
            .Include(x => x.Fixtures).ThenInclude(f => f.Player4)
            .Include(x => x.Fixtures).ThenInclude(f => f.HomeTeam)
            .Include(x => x.Fixtures).ThenInclude(f => f.AwayTeam)
            .Include(x => x.Fixtures).ThenInclude(f => f.Rubbers).ThenInclude(r => r.HomePlayer1)
            .Include(x => x.Fixtures).ThenInclude(f => f.Rubbers).ThenInclude(r => r.HomePlayer2)
            .Include(x => x.Fixtures).ThenInclude(f => f.Rubbers).ThenInclude(r => r.AwayPlayer1)
            .Include(x => x.Fixtures).ThenInclude(f => f.Rubbers).ThenInclude(r => r.AwayPlayer2)
            .Include(x => x.Teams).ThenInclude(t => t.Captain)
            .FirstOrDefaultAsync(x => x.CompetitionID == id, ct);

        if (c is null) return null;

        var user = httpContextAccessor.HttpContext?.User ?? new ClaimsPrincipal();
        if (!await authzService.CanViewCompetitionAsync(user, id))
            return null;

        var courtPairs = await db.CourtPairs
            .AsNoTracking()
            .Where(cp => cp.CompetitionId == id)
            .Include(cp => cp.Court1)
            .Include(cp => cp.Court2)
            .OrderBy(cp => cp.Name)
            .ToListAsync(ct);

        int? myPlayerId = null;
        if (!string.IsNullOrEmpty(currentUser.UserId))
        {
            myPlayerId = c.Participants
                .FirstOrDefault(p => p.Player?.UserId == currentUser.UserId)?.PlayerId;
        }

        return new CompetitionDetailDto(
            c.CompetitionID, c.CompetitionName,
            c.CompetitionFormat,
            c.MaxParticipants, c.StartDate, c.EndDate, c.MaxAge, c.MinAge,
            c.EligibleSex,
            c.HostClubId, c.HostClub?.Name, c.RulesSetId, c.Rules?.Name,
            c.EventId, c.Event?.Name,
            c.Stages.Select(s => new CompetitionStageDto(
                s.CompetitionStageId, s.Name, s.StageOrder,
                s.StageType)).ToList(),
            c.Participants.Select(p => new CompetitionParticipantDto(
                p.PlayerId, p.Player?.DisplayName ?? "",
                p.Status,
                p.RegisteredAt, p.TeamId, p.Team?.Name,
                p.Player?.MobileNumber,
                p.Role,
                p.Player?.Sex,
                p.CompetitionDivisionId,
                p.Division?.Name)).ToList(),
            c.IsArchived,
            c.IsStarted,
            c.CreatorUserId,
            c.IsRestricted,
            c.AllowedPlayers.Select(ap => ap.PlayerId).ToList(),
            c.HasDivisions,
            c.Divisions.OrderBy(d => d.Rank)
                .Select(d => new CompetitionDivisionDto(d.CompetitionDivisionId, d.CompetitionId, d.Rank, d.Name))
                .ToList(),
            c.Fixtures
                .OrderBy(f => f.RoundNumber).ThenBy(f => f.ScheduledAt)
                .Select(ToFixtureDto)
                .ToList(),
            c.Teams
                .Select(t => new CompetitionTeamDto(
                    t.CompetitionTeamId, t.CompetitionId, t.Name,
                    t.CaptainPlayerId, t.Captain?.DisplayName,
                    t.CompetitionDivisionId))
                .ToList(),
            courtPairs.Select(cp => new CourtPairDto(
                cp.CourtPairId, cp.CompetitionId,
                cp.Court1Id, cp.Court1.Name,
                cp.Court2Id, cp.Court2.Name,
                cp.Name)).ToList(),
            c.LeagueScoring,
            myPlayerId);
    }

    private static CompetitionFixtureDto ToFixtureDto(DropShot.Models.CompetitionFixture f) => new(
        f.CompetitionFixtureId, f.CompetitionId,
        f.CompetitionStageId, f.Stage?.Name,
        f.CourtId, f.Court?.Name,
        f.ScheduledAt, f.Status,
        f.FixtureLabel, f.RoundNumber,
        f.Player1Id, f.Player1?.DisplayName,
        f.Player2Id, f.Player2?.DisplayName,
        f.Player3Id, f.Player3?.DisplayName,
        f.Player4Id, f.Player4?.DisplayName,
        f.ResultSummary, f.WinnerPlayerId,
        f.HomeTeamId, f.HomeTeam?.Name,
        f.AwayTeamId, f.AwayTeam?.Name,
        f.WinnerTeamId, f.CourtPairId, f.CourtPair?.Name,
        f.Rubbers
            .OrderBy(r => r.Order)
            .Select(r => new RubberDto(
                r.RubberId, r.CompetitionFixtureId, r.Order, r.Name, r.CourtNumber,
                r.HomeRoles, r.AwayRoles,
                r.HomePlayer1Id, r.HomePlayer1?.DisplayName,
                r.HomePlayer2Id, r.HomePlayer2?.DisplayName,
                r.AwayPlayer1Id, r.AwayPlayer1?.DisplayName,
                r.AwayPlayer2Id, r.AwayPlayer2?.DisplayName,
                r.HomeGames, r.AwayGames, r.WinnerTeamId,
                r.IsComplete, r.SavedMatchId,
                r.HomeSetsWon, r.AwaySetsWon, r.HomeGamesTotal, r.AwayGamesTotal))
            .ToList(),
        f.CompletedAt, f.OriginalResultSummary, f.ResultModifiedByAdmin);

    public async Task SelfRegisterAsync(int competitionId, DropShot.Shared.ParticipantStatus status, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(currentUser.UserId))
            throw new InvalidOperationException("Not authenticated.");

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var player = await db.Players.FirstOrDefaultAsync(p => p.UserId == currentUser.UserId, ct)
            ?? throw new KeyNotFoundException("Could not find your player record.");

        var existing = await db.CompetitionParticipants
            .AnyAsync(cp => cp.CompetitionId == competitionId && cp.PlayerId == player.PlayerId, ct);
        if (existing)
            throw new InvalidOperationException("You are already registered for this competition.");

        db.CompetitionParticipants.Add(new CompetitionParticipant
        {
            CompetitionId = competitionId,
            PlayerId = player.PlayerId,
            Status = status,
            RegisteredAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync(ct);
    }

    public async Task ConfirmParticipationAsync(int competitionId, DropShot.Shared.ParticipantStatus status, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(currentUser.UserId))
            throw new InvalidOperationException("Not authenticated.");

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var participant = await db.CompetitionParticipants
            .FirstOrDefaultAsync(cp => cp.CompetitionId == competitionId
                && cp.Player!.UserId == currentUser.UserId, ct)
            ?? throw new KeyNotFoundException("Could not find your participant record.");

        participant.Status = status;
        await db.SaveChangesAsync(ct);
    }

    public async Task ApproveFixtureResultAsync(
        int fixtureId, ApproveFixtureResultRequest request, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var fx = await db.CompetitionFixtures
            .Include(f => f.Competition)
            .FirstOrDefaultAsync(f => f.CompetitionFixtureId == fixtureId, ct)
            ?? throw new KeyNotFoundException($"Fixture {fixtureId} not found.");

        if (request.OverrideScores is { } o)
        {
            fx.OriginalResultSummary = fx.ResultSummary;
            fx.OriginalWinnerPlayerId = fx.WinnerPlayerId;
            fx.ResultModifiedByAdmin = true;
            fx.ResultSummary = o.ResultSummary;
            fx.WinnerPlayerId = o.WinnerPlayerId;
            fx.HomeSetsWon = o.HomeSetsWon;
            fx.AwaySetsWon = o.AwaySetsWon;
            fx.HomeGamesTotal = o.HomeGamesTotal;
            fx.AwayGamesTotal = o.AwayGamesTotal;
        }

        fx.Status = FixtureStatus.Completed;
        fx.CompletedAt = DateTime.UtcNow;
        fx.VerificationToken = null;

        await db.SaveChangesAsync(ct);
        await CompetitionProgressionService.TryAdvanceAsync(db, fx.CompetitionId, fx.CompetitionFixtureId);
    }

    public async Task SubmitFixtureScoreAsync(
        int fixtureId, SubmitFixtureScoreRequest request, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var fx = await db.CompetitionFixtures
            .Include(f => f.Competition)
            .Include(f => f.Player1)
            .Include(f => f.Player2)
            .Include(f => f.Player3)
            .Include(f => f.Player4)
            .FirstOrDefaultAsync(f => f.CompetitionFixtureId == fixtureId, ct)
            ?? throw new KeyNotFoundException($"Fixture {fixtureId} not found.");

        if (request.AdminOverride && fx.ResultSummary != null)
        {
            fx.OriginalResultSummary = fx.ResultSummary;
            fx.OriginalWinnerPlayerId = fx.WinnerPlayerId;
            fx.ResultModifiedByAdmin = true;
        }

        fx.ResultSummary = request.ResultSummary;
        fx.WinnerPlayerId = request.WinnerPlayerId;
        fx.HomeSetsWon = request.HomeSetsWon;
        fx.AwaySetsWon = request.AwaySetsWon;
        fx.HomeGamesTotal = request.HomeGamesTotal;
        fx.AwayGamesTotal = request.AwayGamesTotal;

        bool requireVerification = !request.AdminOverride && (fx.Competition?.RequireVerification ?? false);
        if (requireVerification)
        {
            fx.Status = FixtureStatus.AwaitingVerification;
            fx.VerificationToken = Guid.NewGuid();
        }
        else
        {
            fx.Status = FixtureStatus.Completed;
            fx.VerificationToken = null;
        }
        fx.CompletedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);

        var competitionId = fx.CompetitionId;
        var savedFixtureId = fx.CompetitionFixtureId;

        if (requireVerification)
        {
            backgroundTasks.Run("fixture-verification-emails", async sp =>
            {
                var svc = sp.GetRequiredService<ResultVerificationService>();
                var dbf = sp.GetRequiredService<IDbContextFactory<MyDbContext>>();
                await using var bgDb = dbf.CreateDbContext();
                var fxBg = await bgDb.CompetitionFixtures
                    .Include(f => f.Competition)
                    .Include(f => f.Player1)
                    .Include(f => f.Player2)
                    .Include(f => f.Player3)
                    .Include(f => f.Player4)
                    .FirstOrDefaultAsync(f => f.CompetitionFixtureId == savedFixtureId);
                if (fxBg is null) return;
                var adminEmails = await svc.GetAdminEmailsForCompetitionAsync(competitionId, bgDb);
                await Task.WhenAll(
                    svc.SendResultNotificationAsync(fxBg),
                    svc.SendAdminVerificationEmailsAsync(fxBg, adminEmails));
            });
        }
        else
        {
            backgroundTasks.Run("fixture-result-notification", async sp =>
            {
                var svc = sp.GetRequiredService<ResultVerificationService>();
                var dbf = sp.GetRequiredService<IDbContextFactory<MyDbContext>>();
                await using var bgDb = dbf.CreateDbContext();
                var fxBg = await bgDb.CompetitionFixtures
                    .Include(f => f.Competition)
                    .Include(f => f.Player1)
                    .Include(f => f.Player2)
                    .Include(f => f.Player3)
                    .Include(f => f.Player4)
                    .FirstOrDefaultAsync(f => f.CompetitionFixtureId == savedFixtureId);
                if (fxBg is null) return;
                await svc.SendResultNotificationAsync(fxBg);
            });

            await CompetitionProgressionService.TryAdvanceAsync(db, competitionId, savedFixtureId);
        }
    }

    public async Task<FixtureRubberContextDto?> GetFixtureRubberContextAsync(int fixtureId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var fx = await db.CompetitionFixtures
            .AsSplitQuery()
            .AsNoTracking()
            .Include(f => f.Competition)
            .Include(f => f.HomeTeam)
            .Include(f => f.AwayTeam)
            .Include(f => f.Rubbers).ThenInclude(r => r.HomePlayer1)
            .Include(f => f.Rubbers).ThenInclude(r => r.HomePlayer2)
            .Include(f => f.Rubbers).ThenInclude(r => r.AwayPlayer1)
            .Include(f => f.Rubbers).ThenInclude(r => r.AwayPlayer2)
            .FirstOrDefaultAsync(f => f.CompetitionFixtureId == fixtureId, ct);
        if (fx is null) return null;

        var comp = fx.Competition;
        var rubbers = fx.Rubbers
            .OrderBy(r => r.Order)
            .Select(r => new RubberDialogDto(
                r.RubberId, r.Order, r.Name, r.CourtNumber,
                r.HomePlayer1Id, r.HomePlayer1?.DisplayName,
                r.HomePlayer2Id, r.HomePlayer2?.DisplayName,
                r.AwayPlayer1Id, r.AwayPlayer1?.DisplayName,
                r.AwayPlayer2Id, r.AwayPlayer2?.DisplayName,
                r.IsComplete, r.SavedMatchId,
                r.SetScores.Select(s => new RubberSetScoreDto(s.Home, s.Away)).ToList(),
                r.WinnerTeamId, r.HomeGames, r.AwayGames,
                r.HomeSetsWon, r.AwaySetsWon, r.HomeGamesTotal, r.AwayGamesTotal))
            .ToList();

        return new FixtureRubberContextDto(
            fx.CompetitionFixtureId,
            fx.CompetitionId,
            comp?.CompetitionName,
            fx.FixtureLabel,
            fx.HomeTeamId,
            fx.AwayTeamId,
            fx.HomeTeam?.Name ?? "Home",
            fx.AwayTeam?.Name ?? "Away",
            comp?.MatchFormat ?? MatchFormatType.BestOf,
            comp?.BestOf ?? 3,
            comp?.NumberOfSets ?? 3,
            comp?.GamesPerSet ?? 6,
            comp?.SetWinMode ?? SetWinMode.WinBy2,
            comp?.RequireVerification ?? false,
            fx.Status == FixtureStatus.AwaitingVerification
                || fx.Status == FixtureStatus.Completed,
            rubbers,
            comp?.LeagueScoring ?? LeagueScoringMode.WinPoints,
            comp?.HostClubId);
    }

    public async Task EnsureFixtureRubbersAsync(int fixtureId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        try
        {
            await rubberResolver.EnsureRubbersAsync(db, fixtureId);
        }
        catch (RubberResolutionException ex)
        {
            throw new InvalidOperationException(ex.Message);
        }
    }

    public async Task SubmitRubberScoresAsync(
        int fixtureId, SubmitRubberScoresRequest request, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var rubIds = request.Scores.Select(s => s.RubberId).ToList();
        var dbRubbers = await db.Rubbers
            .Include(r => r.Fixture)
            .Where(r => r.CompetitionFixtureId == fixtureId && rubIds.Contains(r.RubberId))
            .ToListAsync(ct);

        if (dbRubbers.Count != request.Scores.Count)
            throw new KeyNotFoundException("One or more rubbers were not found on this fixture.");

        foreach (var entry in request.Scores)
        {
            var rub = dbRubbers.First(x => x.RubberId == entry.RubberId);
            rub.IsComplete = true;
            rub.HomeSetsWon = entry.HomeSetsWon;
            rub.AwaySetsWon = entry.AwaySetsWon;
            rub.HomeGamesTotal = entry.HomeGamesTotal;
            rub.AwayGamesTotal = entry.AwayGamesTotal;
            rub.HomeGames = entry.LastSetHomeGames;
            rub.AwayGames = entry.LastSetAwayGames;
            rub.WinnerTeamId = entry.HomeSetsWon > entry.AwaySetsWon ? rub.Fixture.HomeTeamId
                             : entry.AwaySetsWon > entry.HomeSetsWon ? rub.Fixture.AwayTeamId
                             : (int?)null;
            rub.SavedMatchId = null;
            rub.SetScoresJson = System.Text.Json.JsonSerializer.Serialize(
                entry.SetScores.Select(s => new[] { s.Home, s.Away }).ToList());
        }

        await db.SaveChangesAsync(ct);

        // Finalisation cascade — same flow used by RubberScoreDialog.SubmitAsync
        // and BulkRubberScoreDialog.SubmitAsync before the move into this service.
        var allRubbers = await db.Rubbers
            .Include(r => r.HomePlayer1).Include(r => r.HomePlayer2)
            .Include(r => r.AwayPlayer1).Include(r => r.AwayPlayer2)
            .Where(r => r.CompetitionFixtureId == fixtureId)
            .ToListAsync(ct);

        var fx = await db.CompetitionFixtures
            .Include(f => f.Competition)
            .Include(f => f.HomeTeam).ThenInclude(t => t!.Division)
            .Include(f => f.AwayTeam).ThenInclude(t => t!.Division)
            .FirstOrDefaultAsync(f => f.CompetitionFixtureId == fixtureId, ct);
        if (fx is null) return;

        if (fx.HomeTeamId.HasValue && fx.AwayTeamId.HasValue
            && RubberResolutionService.AllComplete(allRubbers))
        {
            var (homeScore, awayScore) = RubberResolutionService.ComputeScore(
                allRubbers, fx.HomeTeamId.Value, fx.AwayTeamId.Value);

            bool alreadyFinalised = fx.Status == FixtureStatus.AwaitingVerification
                || fx.Status == FixtureStatus.Completed;

            fx.ResultSummary = $"{homeScore}-{awayScore}";
            int? winner = homeScore > awayScore ? fx.HomeTeamId
                        : awayScore > homeScore ? fx.AwayTeamId
                        : null;

            if (!winner.HasValue && homeScore == awayScore)
            {
                var mode = fx.Competition?.RubberTieBreak ?? DropShot.Shared.RubberTieBreakMode.AdminDecides;
                winner = await RubberResolutionService.ResolveTieBreakAsync(db, fx, allRubbers, mode);
            }
            fx.WinnerTeamId = winner;

            if (request.AdminOverride && alreadyFinalised)
            {
                // Admin correcting an already-finalised fixture — update aggregates
                // in place. Don't regenerate the verification token (the outstanding
                // admin link must keep working) and don't resend emails.
                await db.SaveChangesAsync(ct);
                return;
            }

            fx.CompletedAt = DateTime.UtcNow;
            bool requireVerification = !request.AdminOverride
                && (fx.Competition?.RequireVerification ?? false);

            if (requireVerification)
            {
                fx.Status = FixtureStatus.AwaitingVerification;
                fx.VerificationToken = Guid.NewGuid();
                await db.SaveChangesAsync(ct);

                var compId = fx.CompetitionId;
                var savedFixtureId = fx.CompetitionFixtureId;
                backgroundTasks.Run("team-match-verification-emails", async sp =>
                {
                    var svc = sp.GetRequiredService<ResultVerificationService>();
                    var dbf = sp.GetRequiredService<IDbContextFactory<MyDbContext>>();
                    await using var bgDb = dbf.CreateDbContext();
                    var fxBg = await bgDb.CompetitionFixtures
                        .Include(f => f.Competition)
                        .Include(f => f.HomeTeam).Include(f => f.AwayTeam)
                        .FirstOrDefaultAsync(f => f.CompetitionFixtureId == savedFixtureId);
                    if (fxBg is null) return;
                    var rubsBg = await bgDb.Rubbers
                        .Include(r => r.HomePlayer1).Include(r => r.HomePlayer2)
                        .Include(r => r.AwayPlayer1).Include(r => r.AwayPlayer2)
                        .Where(r => r.CompetitionFixtureId == savedFixtureId)
                        .ToListAsync();
                    var adminEmails = await svc.GetAdminEmailsForCompetitionAsync(compId, bgDb);
                    await svc.SendAdminVerificationEmailsForTeamMatchAsync(fxBg, rubsBg, adminEmails);
                });
            }
            else
            {
                fx.Status = FixtureStatus.Completed;
                await db.SaveChangesAsync(ct);

                var savedFixtureId = fx.CompetitionFixtureId;
                backgroundTasks.Run("team-match-result-notification", async sp =>
                {
                    var svc = sp.GetRequiredService<ResultVerificationService>();
                    var dbf = sp.GetRequiredService<IDbContextFactory<MyDbContext>>();
                    await using var bgDb = dbf.CreateDbContext();
                    var fxBg = await bgDb.CompetitionFixtures
                        .Include(f => f.Competition)
                        .Include(f => f.HomeTeam).Include(f => f.AwayTeam)
                        .FirstOrDefaultAsync(f => f.CompetitionFixtureId == savedFixtureId);
                    if (fxBg is null) return;
                    var rubsBg = await bgDb.Rubbers
                        .Include(r => r.HomePlayer1).Include(r => r.HomePlayer2)
                        .Include(r => r.AwayPlayer1).Include(r => r.AwayPlayer2)
                        .Where(r => r.CompetitionFixtureId == savedFixtureId)
                        .ToListAsync();
                    await svc.SendResultNotificationForTeamMatchAsync(fxBg, rubsBg);
                });

                await CompetitionProgressionService.TryAdvanceAsync(db, fx.CompetitionId, fx.CompetitionFixtureId);
            }
        }
        else
        {
            // Rubbers are in progress but not all done yet — flip Scheduled →
            // InProgress now that the first score has been recorded. EnsureRubbersAsync
            // deliberately leaves Scheduled until an actual score lands.
            if (fx.Status == FixtureStatus.Scheduled)
            {
                fx.Status = FixtureStatus.InProgress;
                await db.SaveChangesAsync(ct);
            }
        }
    }

    private static CompetitionDto ToDto(Competition c) => new(
        c.CompetitionID, c.CompetitionName,
        c.CompetitionFormat,
        c.MaxParticipants, c.StartDate, c.EndDate, c.MaxAge, c.MinAge,
        c.EligibleSex,
        c.HostClubId, c.HostClub?.Name, c.RulesSetId, c.Rules?.Name,
        c.EventId, c.Event?.Name, c.IsArchived, c.IsStarted,
        c.CreatorUserId, c.IsRestricted, c.RegisterByDate);

    public async Task<MyCompetitionsViewDto> GetMyCompetitionsViewAsync(CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(currentUser.UserId))
            return new MyCompetitionsViewDto(false, [], []);

        await using var db = await dbFactory.CreateDbContextAsync(ct);

        // A user can have multiple Player rows in flight: a verified row
        // (IsLight=false) plus light rows that were created by an admin
        // adding them to a competition before they registered, then later
        // linked to their account. CompetitionParticipants entries can
        // reference either. Filtering to !IsLight here was making My
        // Competitions come back empty for users with light-only
        // participation history while Available still worked (because
        // Available just needs *a* player for eligibility checks).
        // Match on UserId for the entered query; pick the verified
        // player (or first if none) for eligibility.
        var myPlayers = await db.Players
            .Where(p => p.UserId == currentUser.UserId)
            .ToListAsync(ct);
        if (myPlayers.Count == 0) return new MyCompetitionsViewDto(false, [], []);
        var myPlayerIds = myPlayers.Select(p => p.PlayerId).ToList();
        var eligibilityPlayer = myPlayers.FirstOrDefault(p => !p.IsLight) ?? myPlayers[0];

        var enteredIds = await db.CompetitionParticipants
            .Where(cp => myPlayerIds.Contains(cp.PlayerId))
            .Select(cp => cp.CompetitionId)
            .Distinct()
            .ToListAsync(ct);

        var enteredEntities = await db.Competition
            .Include(c => c.HostClub)
            .Include(c => c.Event)
            .Where(c => enteredIds.Contains(c.CompetitionID))
            .OrderBy(c => c.StartDate).ThenBy(c => c.CompetitionName)
            .ToListAsync(ct);

        var today = DateTime.UtcNow.Date;
        var candidates = await db.Competition
            .Include(c => c.HostClub)
            .Include(c => c.Event)
            .Include(c => c.AllowedPlayers)
            .Where(c => !enteredIds.Contains(c.CompetitionID) && !c.IsArchived && !c.IsStarted)
            .Where(c => !c.StartDate.HasValue || c.StartDate.Value >= today)
            .Where(c => !c.RegisterByDate.HasValue || c.RegisterByDate.Value >= today)
            .OrderBy(c => c.StartDate).ThenBy(c => c.CompetitionName)
            .ToListAsync(ct);

        var available = candidates
            .Where(c => EligibilityEvaluator.Evaluate(c, eligibilityPlayer).Count == 0)
            .Select(ToDto).ToList();
        return new MyCompetitionsViewDto(true, enteredEntities.Select(ToDto).ToList(), available);
    }

    public async Task<List<CompetitionFixtureDto>> GetPendingVerificationFixturesAsync(CancellationToken ct = default)
    {
        var user = httpContextAccessor.HttpContext?.User ?? new ClaimsPrincipal();
        var isAdmin = await authzService.IsAdminAsync(user);
        var editableClubIds = new HashSet<int>(await authzService.GetAdminClubIdsAsync(user));
        var competitionAdminIds = await authzService.GetEditableCompetitionIdsAsync(user);

        if (!isAdmin && editableClubIds.Count == 0 && competitionAdminIds.Count == 0)
            return [];

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var fixtures = await db.CompetitionFixtures
            .Include(f => f.Competition)
            .Include(f => f.Player1).Include(f => f.Player2)
            .Include(f => f.Player3).Include(f => f.Player4)
            .Include(f => f.HomeTeam).Include(f => f.AwayTeam)
            .Where(f => f.Status == FixtureStatus.AwaitingVerification
                && (isAdmin
                    || (f.Competition.HostClubId.HasValue && editableClubIds.Contains(f.Competition.HostClubId.Value))
                    || competitionAdminIds.Contains(f.CompetitionId)))
            .OrderBy(f => f.Competition.CompetitionName)
            .ThenBy(f => f.ScheduledAt)
            .ToListAsync(ct);

        return fixtures.Select(f => ToFixtureDto(f) with { CompetitionName = f.Competition?.CompetitionName }).ToList();
    }

    public async Task<List<CompetitionFixtureDto>> GetMyUpcomingFixturesAsync(CancellationToken ct = default)
    {
        var (db, pid, myTeamIds) = await ResolveCurrentPlayerAsync(ct);
        if (db is null || pid is null) return [];
        await using (db)
        {
            var fixtures = await db.CompetitionFixtures
                .Include(f => f.Competition)
                .Include(f => f.Stage)
                .Include(f => f.Player1).Include(f => f.Player2)
                .Include(f => f.Player3).Include(f => f.Player4)
                .Include(f => f.HomeTeam).Include(f => f.AwayTeam)
                .Where(f => (f.Player1Id == pid || f.Player2Id == pid
                             || f.Player3Id == pid || f.Player4Id == pid
                             || (f.HomeTeamId.HasValue && myTeamIds.Contains(f.HomeTeamId.Value))
                             || (f.AwayTeamId.HasValue && myTeamIds.Contains(f.AwayTeamId.Value)))
                         && (f.Status == FixtureStatus.Scheduled
                             || f.Status == FixtureStatus.InProgress))
                .OrderBy(f => f.ScheduledAt)
                .ToListAsync(ct);

            return fixtures
                .Select(f => ToFixtureDto(f) with { CompetitionName = f.Competition?.CompetitionName })
                .ToList();
        }
    }

    public async Task<List<CompetitionFixtureDto>> GetMyRecentCompletedFixturesAsync(
        int limit = 6, CancellationToken ct = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 50);
        var (db, pid, myTeamIds) = await ResolveCurrentPlayerAsync(ct);
        if (db is null || pid is null) return [];
        await using (db)
        {
            var fixtures = await db.CompetitionFixtures
                .Include(f => f.Competition)
                .Include(f => f.Player1).Include(f => f.Player2)
                .Include(f => f.Player3).Include(f => f.Player4)
                .Include(f => f.HomeTeam).Include(f => f.AwayTeam)
                .Where(f => (f.Player1Id == pid || f.Player2Id == pid
                             || f.Player3Id == pid || f.Player4Id == pid
                             || (f.HomeTeamId.HasValue && myTeamIds.Contains(f.HomeTeamId.Value))
                             || (f.AwayTeamId.HasValue && myTeamIds.Contains(f.AwayTeamId.Value)))
                         && f.Status == FixtureStatus.Completed
                         && f.ResultSummary != null)
                .OrderByDescending(f => f.CompletedAt ?? f.ScheduledAt)
                .Take(safeLimit)
                .ToListAsync(ct);

            return fixtures
                .Select(f => ToFixtureDto(f) with { CompetitionName = f.Competition?.CompetitionName })
                .ToList();
        }
    }

    /// <summary>
    /// Loads the current user's player ID and the teams they're a member of.
    /// Returns (null, null, []) when there's no signed-in user or no Player
    /// row maps to the UserId — callers short-circuit to an empty result.
    /// The DbContext is returned so the caller can keep using it (saves the
    /// second CreateDbContextAsync round-trip).
    /// </summary>
    private async Task<(MyDbContext? Db, int? PlayerId, List<int> MyTeamIds)>
        ResolveCurrentPlayerAsync(CancellationToken ct)
    {
        var userId = currentUser.UserId;
        if (string.IsNullOrEmpty(userId)) return (null, null, []);

        var db = await dbFactory.CreateDbContextAsync(ct);
        var pid = await db.Players
            .Where(p => p.UserId == userId)
            .Select(p => (int?)p.PlayerId)
            .FirstOrDefaultAsync(ct);
        if (pid is null)
        {
            await db.DisposeAsync();
            return (null, null, []);
        }

        var teamIds = await db.CompetitionParticipants
            .Where(cp => cp.PlayerId == pid && cp.TeamId != null)
            .Select(cp => cp.TeamId!.Value)
            .ToListAsync(ct);
        return (db, pid, teamIds);
    }

    public async Task ToggleArchiveAsync(int competitionId, CancellationToken ct = default)
    {
        var user = httpContextAccessor.HttpContext?.User ?? new ClaimsPrincipal();
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var entity = await db.Competition.FindAsync(new object?[] { competitionId }, ct)
            ?? throw new KeyNotFoundException($"Competition {competitionId} not found.");
        if (!await authzService.CanEditCompetitionAsync(user, entity.HostClubId, entity.CompetitionID))
            throw new UnauthorizedAccessException("You can't edit this competition.");

        entity.IsArchived = !entity.IsArchived;
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteCompetitionAsync(int competitionId, CancellationToken ct = default)
    {
        var user = httpContextAccessor.HttpContext?.User ?? new ClaimsPrincipal();
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var entity = await db.Competition.FindAsync(new object?[] { competitionId }, ct)
            ?? throw new KeyNotFoundException($"Competition {competitionId} not found.");
        if (!await authzService.CanEditCompetitionAsync(user, entity.HostClubId, entity.CompetitionID))
            throw new UnauthorizedAccessException("You can't edit this competition.");
        if (!entity.IsArchived)
            throw new InvalidOperationException("Competition must be archived before it can be deleted.");

        var rubbers = await db.Rubbers
            .Where(r => r.Fixture.CompetitionId == competitionId)
            .ToListAsync(ct);
        db.Rubbers.RemoveRange(rubbers);

        var fixtures = await db.CompetitionFixtures
            .Where(f => f.CompetitionId == competitionId)
            .ToListAsync(ct);
        db.CompetitionFixtures.RemoveRange(fixtures);

        db.Competition.Remove(entity);
        await db.SaveChangesAsync(ct);
    }

    public async Task EnterCompetitionAsync(int competitionId, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(currentUser.UserId))
            throw new InvalidOperationException("Not authenticated.");

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var player = await db.Players
            .FirstOrDefaultAsync(p => p.UserId == currentUser.UserId && !p.IsLight, ct)
            ?? throw new InvalidOperationException("You need a player profile before entering competitions.");

        var comp = await db.Competition
            .Include(c => c.AllowedPlayers)
            .FirstOrDefaultAsync(c => c.CompetitionID == competitionId, ct)
            ?? throw new KeyNotFoundException($"Competition {competitionId} not found.");

        var today = DateTime.UtcNow.Date;
        if (comp.IsStarted)
            throw new InvalidOperationException("This competition has already started.");
        if (comp.StartDate.HasValue && comp.StartDate.Value < today)
            throw new InvalidOperationException("This competition has already started.");
        if (comp.RegisterByDate.HasValue && comp.RegisterByDate.Value < today)
            throw new InvalidOperationException("Registration for this competition has closed.");

        var violations = EligibilityEvaluator.Evaluate(comp, player);
        if (violations.Count > 0)
            throw new InvalidOperationException($"You're not eligible for this competition: {violations[0].Message}");

        if (comp.MaxParticipants.HasValue)
        {
            var currentCount = await db.CompetitionParticipants
                .CountAsync(cp => cp.CompetitionId == competitionId, ct);
            if (currentCount >= comp.MaxParticipants.Value)
                throw new InvalidOperationException("This competition is full.");
        }

        var alreadyEntered = await db.CompetitionParticipants
            .AnyAsync(cp => cp.CompetitionId == competitionId && cp.PlayerId == player.PlayerId, ct);
        if (alreadyEntered)
            throw new InvalidOperationException("You're already entered in this competition.");

        db.CompetitionParticipants.Add(new CompetitionParticipant
        {
            CompetitionId = competitionId,
            PlayerId = player.PlayerId,
            RegisteredAt = DateTime.UtcNow,
            Status = ParticipantStatus.Registered
        });
        await db.SaveChangesAsync(ct);
    }

    public async Task<VerifyFixtureViewDto?> GetFixtureForVerificationAsync(Guid token, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var fx = await db.CompetitionFixtures
            .AsNoTracking()
            .Include(f => f.Competition)
            .Include(f => f.HomeTeam)
            .Include(f => f.AwayTeam)
            .Include(f => f.Player1).Include(f => f.Player2)
            .Include(f => f.Player3).Include(f => f.Player4)
            .FirstOrDefaultAsync(f => f.VerificationToken == token
                                   && f.Status == FixtureStatus.AwaitingVerification, ct);
        if (fx is null) return null;

        bool isTeamMatch = fx.HomeTeamId.HasValue || fx.AwayTeamId.HasValue;
        string side1, side2;
        int bestOf = 3;
        var rubbers = new List<VerifyRubberDto>();
        int aggHome = 0, aggAway = 0;
        string aggUnit = "rubbers";
        string secondary = "";
        bool allRubbersComplete = false;

        if (isTeamMatch)
        {
            side1 = fx.HomeTeam?.Name ?? "Home";
            side2 = fx.AwayTeam?.Name ?? "Away";

            var rubberRows = await db.Rubbers
                .AsNoTracking()
                .Include(r => r.HomePlayer1).Include(r => r.HomePlayer2)
                .Include(r => r.AwayPlayer1).Include(r => r.AwayPlayer2)
                .Where(r => r.CompetitionFixtureId == fx.CompetitionFixtureId)
                .OrderBy(r => r.Order)
                .ToListAsync(ct);

            if (fx.HomeTeamId.HasValue && fx.AwayTeamId.HasValue)
            {
                var mode = fx.Competition?.LeagueScoring ?? LeagueScoringMode.WinPoints;
                (aggHome, aggAway, aggUnit) = RubberResolutionService.ComputeLeagueScore(
                    rubberRows, fx.HomeTeamId.Value, fx.AwayTeamId.Value, mode);

                var completed = rubberRows.Where(r => r.IsComplete).ToList();
                int rubbersHome = completed.Count(r => r.WinnerTeamId == fx.HomeTeamId);
                int rubbersAway = completed.Count(r => r.WinnerTeamId == fx.AwayTeamId);
                int setsHome = completed.Sum(r => r.HomeSetsWon ?? 0);
                int setsAway = completed.Sum(r => r.AwaySetsWon ?? 0);
                int gamesHome = completed.Sum(r => r.HomeGamesTotal ?? 0);
                int gamesAway = completed.Sum(r => r.AwayGamesTotal ?? 0);
                secondary = mode switch
                {
                    LeagueScoringMode.SetsWon  => $"Rubbers: {rubbersHome}–{rubbersAway} · Games: {gamesHome}–{gamesAway}",
                    LeagueScoringMode.GamesWon => $"Rubbers: {rubbersHome}–{rubbersAway} · Sets: {setsHome}–{setsAway}",
                    _                          => $"Sets: {setsHome}–{setsAway} · Games: {gamesHome}–{gamesAway}",
                };
            }
            allRubbersComplete = rubberRows.Count > 0 && rubberRows.All(r => r.IsComplete);

            foreach (var r in rubberRows)
            {
                var homePair = string.Join(" & ",
                    new[] { r.HomePlayer1?.DisplayName, r.HomePlayer2?.DisplayName }.Where(n => !string.IsNullOrEmpty(n)));
                var awayPair = string.Join(" & ",
                    new[] { r.AwayPlayer1?.DisplayName, r.AwayPlayer2?.DisplayName }.Where(n => !string.IsNullOrEmpty(n)));
                IReadOnlyList<RubberSetScoreDto> setScores =
                    r.SetScores.Select(s => new RubberSetScoreDto(s.Home, s.Away)).ToList();
                rubbers.Add(new VerifyRubberDto(
                    r.RubberId, r.Order, r.Name,
                    string.IsNullOrEmpty(homePair) ? null : homePair,
                    string.IsNullOrEmpty(awayPair) ? null : awayPair,
                    r.HomeSetsWon, r.AwaySetsWon, r.IsComplete, r.WinnerTeamId,
                    setScores));
            }
        }
        else
        {
            var comp = fx.Competition;
            bestOf = comp?.MatchFormat == MatchFormatType.FixedSets
                ? Math.Max(1, comp.NumberOfSets)
                : Math.Max(1, comp?.BestOf ?? 3);
            var s1Parts = new[] { fx.Player1?.DisplayName, fx.Player3?.DisplayName }
                .Where(n => n != null).ToList();
            var s2Parts = new[] { fx.Player2?.DisplayName, fx.Player4?.DisplayName }
                .Where(n => n != null).ToList();
            side1 = s1Parts.Any() ? string.Join(" & ", s1Parts) : "Player 1";
            side2 = s2Parts.Any() ? string.Join(" & ", s2Parts) : "Player 2";
        }

        return new VerifyFixtureViewDto(
            fx.CompetitionFixtureId,
            fx.CompetitionId,
            fx.Competition?.CompetitionName,
            fx.FixtureLabel,
            isTeamMatch,
            side1, side2,
            fx.HomeTeamId, fx.AwayTeamId,
            fx.Player1Id, fx.Player2Id,
            fx.WinnerPlayerId, fx.WinnerTeamId,
            fx.ResultSummary,
            bestOf,
            aggHome, aggAway, aggUnit, secondary,
            allRubbersComplete,
            (fx.Competition?.RubberTieBreak ?? DropShot.Shared.RubberTieBreakMode.AdminDecides).ToString(),
            rubbers);
    }

    public async Task<ApproveFixtureByTokenResultDto> ApproveFixtureByTokenAsync(
        Guid token, ApproveFixtureByTokenRequest request, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var fx = await db.CompetitionFixtures
            .FirstOrDefaultAsync(f => f.VerificationToken == token
                                   && f.Status == FixtureStatus.AwaitingVerification, ct);
        if (fx is null)
            return new ApproveFixtureByTokenResultDto(false,
                "This link has already been used or could not be found.", null, false);

        bool wasModified = false;

        if (request.OverrideScores is { } o)
        {
            fx.OriginalResultSummary = fx.ResultSummary;
            fx.OriginalWinnerPlayerId = fx.WinnerPlayerId;
            fx.ResultModifiedByAdmin = true;
            fx.ResultSummary = o.ResultSummary;
            fx.WinnerPlayerId = o.WinnerPlayerId;
            fx.HomeSetsWon = o.HomeSetsWon;
            fx.AwaySetsWon = o.AwaySetsWon;
            fx.HomeGamesTotal = o.HomeGamesTotal;
            fx.AwayGamesTotal = o.AwayGamesTotal;
            wasModified = true;
        }

        if (request.ManualWinnerTeamId.HasValue && !fx.WinnerTeamId.HasValue)
            fx.WinnerTeamId = request.ManualWinnerTeamId;

        fx.Status = FixtureStatus.Completed;
        fx.CompletedAt = DateTime.UtcNow;
        fx.VerificationToken = null;
        await db.SaveChangesAsync(ct);

        await CompetitionProgressionService.TryAdvanceAsync(db, fx.CompetitionId, fx.CompetitionFixtureId);

        return new ApproveFixtureByTokenResultDto(true, null, fx.CompetitionId, wasModified);
    }
}
