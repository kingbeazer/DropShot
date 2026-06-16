using DropShot.Data;
using DropShot.Models;
using DropShot.Shared;
using DropShot.Shared.Dtos;
using DropShot.UI.Services;
using DropShot.UI.Services.Auth;
using Microsoft.AspNetCore.Components.Authorization;
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
    AuthenticationStateProvider authStateProvider,
    ICurrentUser currentUser,
    BackgroundTaskQueue backgroundTasks,
    RubberResolutionService rubberResolver,
    PlayerRatingService ratings,
    IPhoneVisibilityService phoneVisibility,
    ILogger<WebCompetitionService> logger) : ICompetitionService
{
    /// <summary>
    /// Best-available principal: HttpContext.User on prerender, controllers,
    /// and JWT-bearer (MAUI) requests; AuthenticationStateProvider's user
    /// for Blazor Server SignalR circuits where HttpContext is null. Without
    /// this fallback, every visibility check on an interactive Blazor
    /// circuit ran against an anonymous principal, returning empty lists
    /// even for Admin / SuperAdmin / ClubAdmin.
    /// </summary>
    private async Task<ClaimsPrincipal> GetPrincipalAsync()
    {
        var http = httpContextAccessor.HttpContext?.User;
        if (http?.Identity?.IsAuthenticated == true) return http;
        try
        {
            var state = await authStateProvider.GetAuthenticationStateAsync();
            if (state.User.Identity?.IsAuthenticated == true) return state.User;
        }
        catch
        {
            // ServerAuthenticationStateProvider can throw if its scope's
            // auth-state task hasn't been initialised yet; fall through.
        }
        return http ?? new ClaimsPrincipal();
    }

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

        var user = await GetPrincipalAsync();
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

        var user = await GetPrincipalAsync();
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
        string? myMobileNumber = null;
        if (!string.IsNullOrEmpty(currentUser.UserId))
        {
            myPlayerId = c.Participants
                .FirstOrDefault(p => p.Player?.UserId == currentUser.UserId)?.PlayerId;

            // The caller's own number — separate from the participants
            // projection because the entry-consent dialog needs it before
            // the caller is in the participants list. Always populated for
            // the caller themselves (rule #1 in PhoneVisibilityService).
            // Lazy-sync from ApplicationUser.PhoneNumber if the Player row
            // is empty — covers historical accounts that set their number
            // before the save-time sync was added.
            var callerPlayer = await db.Players
                .FirstOrDefaultAsync(p => p.UserId == currentUser.UserId, ct);
            if (callerPlayer is not null)
                myMobileNumber = await EnsurePlayerMobileSyncedAsync(db, callerPlayer, ct);
        }

        var ratingsByPlayer = await ratings.GetRosterRatingsAsync(id, ct);
        var ratingSuggestions = (await ratings.GetVisibleSuggestionsAsync(id, ct))
            .ToDictionary(s => s.PlayerId);
        var divisionPlacements = (await ratings.SuggestDivisionPlacementsAsync(id, ct))
            .ToDictionary(p => p.PlayerId);
        var rolePlacements = (await ratings.SuggestRolePlacementsAsync(id, ct))
            .ToDictionary(p => p.PlayerId);

        // Decay-event feed for SinglesLadder. Null for other formats so the UI
        // doesn't accidentally render an "activity" panel where none belongs.
        List<LadderInactivityDecayDto>? decayEvents = null;
        if (c.CompetitionFormat == CompetitionFormat.SinglesLadder)
        {
            decayEvents = await db.LadderInactivityDecays
                .Where(d => d.CompetitionId == id)
                .Include(d => d.Player)
                .OrderByDescending(d => d.AppliedAt)
                .Take(50)
                .Select(d => new LadderInactivityDecayDto(
                    d.PlayerId,
                    d.Player.DisplayName,
                    d.AppliedAt,
                    d.RatingBefore,
                    d.RatingAfter,
                    d.DaysInactive))
                .ToListAsync(ct);
        }

        // Strip MobileNumber from the wire payload for players the viewer
        // isn't allowed to see. Server-side gating — hiding it client-side
        // would still leak the value in the JSON response.
        var canEditThisCompetition = await authzService.CanEditCompetitionAsync(user, c.HostClubId, id);
        var visiblePhonePlayerIds = await phoneVisibility.VisiblePhoneNumberPlayerIdsAsync(
            currentUser.UserId ?? "",
            id,
            c.Participants.Select(p => p.PlayerId).ToList(),
            canEditThisCompetition,
            ct);

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
                visiblePhonePlayerIds.Contains(p.PlayerId) ? p.Player?.MobileNumber : null,
                p.Role,
                p.Player?.Sex,
                p.CompetitionDivisionId,
                p.Division?.Name,
                ratingsByPlayer.TryGetValue(p.PlayerId, out var r)
                    ? new PlayerRatingDto(r.Value, r.IsProvisional)
                    : null,
                ratingSuggestions.TryGetValue(p.PlayerId, out var rs)
                    ? new PlayerRatingSuggestionDto(rs.PreviousRating, rs.SuggestedRating, rs.Delta, rs.RubbersPlayed)
                    : null,
                BuildPlacementSuggestion(p.PlayerId, divisionPlacements, rolePlacements),
                p.EloRating,
                p.MatchesPlayed,
                p.IsProvisional,
                p.LastMatchAt)).ToList(),
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
            myPlayerId,
            c.Description,
            c.LadderKFactor,
            c.LadderStartingRating,
            c.LadderProvisionalMatches,
            c.LadderUseMarginOfVictory,
            decayEvents,
            myMobileNumber);
    }

    private static PlacementSuggestionDto? BuildPlacementSuggestion(
        int playerId,
        Dictionary<int, PlayerRatingService.DivisionPlacement> divisions,
        Dictionary<int, PlayerRatingService.RolePlacement> roles)
    {
        var hasDivision = divisions.TryGetValue(playerId, out var d);
        var hasRole = roles.TryGetValue(playerId, out var r);
        if (!hasDivision && !hasRole) return null;
        return new PlacementSuggestionDto(
            SuggestedDivisionId: hasDivision ? d!.SuggestedDivisionId : null,
            SuggestedDivisionName: hasDivision ? d!.SuggestedDivisionName : null,
            SuggestedRole: hasRole ? r!.SuggestedRole : null);
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
                r.HomeSetsWon, r.AwaySetsWon, r.HomeGamesTotal, r.AwayGamesTotal,
                r.SetScores.Select(s => new RubberSetScoreDto(s.Home, s.Away)).ToList()))
            .ToList(),
        f.CompletedAt, f.OriginalResultSummary, f.ResultModifiedByAdmin,
        CompetitionName: null,
        Player1RatingBefore: f.Player1RatingBefore,
        Player1RatingAfter: f.Player1RatingAfter,
        Player2RatingBefore: f.Player2RatingBefore,
        Player2RatingAfter: f.Player2RatingAfter,
        HomeGamesTotal: f.HomeGamesTotal,
        AwayGamesTotal: f.AwayGamesTotal);

    public async Task SelfRegisterAsync(
        int competitionId,
        DropShot.Shared.ParticipantStatus status,
        PhoneShareConsent consent,
        CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(currentUser.UserId))
            throw new InvalidOperationException("Not authenticated.");

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var player = await db.Players.FirstOrDefaultAsync(p => p.UserId == currentUser.UserId, ct)
            ?? throw new KeyNotFoundException("Could not find your player record.");

        await EnsurePlayerMobileSyncedAsync(db, player, ct);
        RequirePhoneAndConsent(player, consent);

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
        db.CompetitionEntryConsents.Add(BuildConsentRow(competitionId, player.PlayerId, consent));
        await db.SaveChangesAsync(ct);
    }

    public async Task ConfirmParticipationAsync(
        int competitionId,
        DropShot.Shared.ParticipantStatus status,
        PhoneShareConsent consent,
        CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(currentUser.UserId))
            throw new InvalidOperationException("Not authenticated.");

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var participant = await db.CompetitionParticipants
            .Include(cp => cp.Player)
            .FirstOrDefaultAsync(cp => cp.CompetitionId == competitionId
                && cp.Player!.UserId == currentUser.UserId, ct)
            ?? throw new KeyNotFoundException("Could not find your participant record.");

        await EnsurePlayerMobileSyncedAsync(db, participant.Player!, ct);
        RequirePhoneAndConsent(participant.Player!, consent);

        participant.Status = status;
        db.CompetitionEntryConsents.Add(BuildConsentRow(competitionId, participant.PlayerId, consent));
        await db.SaveChangesAsync(ct);
    }

    public async Task<LadderSimulationResultDto> SimulateLadderAsync(
        int competitionId, int weeks, int? seed = null, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var r = await LadderSimulationService.SimulateAsync(db, competitionId, weeks, seed, ct);
        return new LadderSimulationResultDto(
            r.Participants, r.ActivePlayers, r.IdlePlayers,
            r.FixturesGenerated, r.DecayEventsGenerated);
    }

    /// <summary>
    /// Lazy-sync helper: if <paramref name="player"/>.MobileNumber is empty
    /// but the linked Identity user has a PhoneNumber, copy it onto the
    /// player row and save. Covers users who set their phone on /Account/Manage
    /// before the save-time sync existed — the two columns weren't kept
    /// aligned, so competition entry (which reads Player.MobileNumber) blocked
    /// them even though their account showed a number. Returns the effective
    /// mobile number after any backfill.
    /// </summary>
    private static async Task<string?> EnsurePlayerMobileSyncedAsync(
        MyDbContext db, Player player, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(player.MobileNumber)) return player.MobileNumber;
        if (string.IsNullOrEmpty(player.UserId)) return null;

        var identityPhone = await db.Users
            .Where(u => u.Id == player.UserId)
            .Select(u => u.PhoneNumber)
            .FirstOrDefaultAsync(ct);
        if (string.IsNullOrWhiteSpace(identityPhone)) return null;

        player.MobileNumber = identityPhone.Trim();
        await db.SaveChangesAsync(ct);
        return player.MobileNumber;
    }

    private static void RequirePhoneAndConsent(Player player, PhoneShareConsent consent)
    {
        if (string.IsNullOrWhiteSpace(player.MobileNumber))
            throw new InvalidOperationException(
                "Add a mobile number to your profile before entering competitions.");
        if (!consent.Granted)
            throw new InvalidOperationException(
                "Consent required to share your mobile number with other competitors.");
        if (string.IsNullOrWhiteSpace(consent.WordingShown))
            throw new InvalidOperationException("Consent wording is required.");
        if (consent.Version != PhoneVisibilityService.CurrentConsentVersion)
            throw new InvalidOperationException(
                "Consent form has been updated — please refresh the page and try again.");
    }

    private static CompetitionEntryConsent BuildConsentRow(
        int competitionId, int playerId, PhoneShareConsent consent) => new()
    {
        CompetitionId = competitionId,
        PlayerId = playerId,
        ConsentGivenUtc = DateTime.UtcNow,
        ConsentWordingShown = consent.WordingShown,
        ConsentVersion = consent.Version,
    };


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

        // Compute whether the calling user is allowed to score this fixture.
        // The pages that exist solely for score entry redirect home when this
        // is false so a logged-in non-participant can't reach them by URL.
        // Mirrors the controller-level check in SubmitRubberScores.
        bool canUserScore = false;
        var principal = await GetPrincipalAsync();
        if (await authzService.CanEditCompetitionAsync(principal, comp?.HostClubId, fx.CompetitionId))
        {
            canUserScore = true;
        }
        else if (!string.IsNullOrEmpty(currentUser.UserId))
        {
            var myPlayerId = await db.Players.AsNoTracking()
                .Where(p => p.UserId == currentUser.UserId)
                .Select(p => (int?)p.PlayerId)
                .FirstOrDefaultAsync(ct);
            if (myPlayerId is { } pid)
            {
                canUserScore = await db.CompetitionParticipants.AsNoTracking()
                    .AnyAsync(cp => cp.CompetitionId == fx.CompetitionId
                        && cp.PlayerId == pid
                        && cp.TeamId.HasValue
                        && (cp.TeamId == fx.HomeTeamId || cp.TeamId == fx.AwayTeamId), ct);
            }
        }

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
            comp?.HostClubId,
            CanUserScore: canUserScore);
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

    public async Task<FixtureScoreContextDto?> GetFixtureScoreContextAsync(int fixtureId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var fx = await db.CompetitionFixtures.AsNoTracking()
            .Include(f => f.Competition)
            .Include(f => f.Stage)
            .Include(f => f.Court)
            .Include(f => f.CourtPair).ThenInclude(cp => cp!.Court1)
            .Include(f => f.CourtPair).ThenInclude(cp => cp!.Court2)
            .Include(f => f.Player1)
            .Include(f => f.Player2)
            .Include(f => f.Player3)
            .Include(f => f.Player4)
            .Include(f => f.HomeTeam)
            .Include(f => f.AwayTeam)
            .FirstOrDefaultAsync(f => f.CompetitionFixtureId == fixtureId, ct);
        if (fx is null) return null;

        var principal = await GetPrincipalAsync();
        var canEdit = await authzService.CanEditCompetitionAsync(principal, fx.Competition?.HostClubId, fx.CompetitionId);
        if (!canEdit)
        {
            // Non-admins must be a participant in this fixture to score it.
            if (string.IsNullOrEmpty(currentUser.UserId))
                throw new UnauthorizedAccessException("Not authenticated.");
            var myPlayerId = await db.Players.AsNoTracking()
                .Where(p => p.UserId == currentUser.UserId)
                .Select(p => (int?)p.PlayerId)
                .FirstOrDefaultAsync(ct);
            var isParticipant = myPlayerId is { } pid
                && (fx.Player1Id == pid || fx.Player2Id == pid || fx.Player3Id == pid || fx.Player4Id == pid);
            if (!isParticipant)
                throw new UnauthorizedAccessException("You can't score this fixture.");
        }

        var dto = ToFixtureDto(fx) with { CompetitionName = fx.Competition?.CompetitionName };
        return new FixtureScoreContextDto(
            dto,
            fx.Competition?.MatchFormat ?? MatchFormatType.BestOf,
            fx.Competition?.NumberOfSets ?? 3,
            fx.Competition?.BestOf ?? 3,
            fx.Competition?.GamesPerSet ?? 6,
            fx.Competition?.SetWinMode ?? SetWinMode.WinBy2,
            canEdit,
            FinalSetTieBreakGames: fx.Competition?.FinalSetTieBreakGames ?? 10,
            FinalSetTieBreakWinMode: fx.Competition?.FinalSetTieBreakWinMode ?? SetWinMode.WinBy2);
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
            var (homeRubbers, awayRubbers) = RubberResolutionService.ComputeScore(
                allRubbers, fx.HomeTeamId.Value, fx.AwayTeamId.Value);

            // ResultSummary should reflect the competition's LeagueScoring mode
            // rather than always showing rubber count. For SetsWon, this keeps
            // the displayed "X-Y" in line with the totalled sets across all
            // rubbers (so a fixture with three tied 1-1 rubbers and one 2-0
            // shows as "5-3" sets, not "1-0" rubbers).
            var scoringMode = fx.Competition?.LeagueScoring ?? LeagueScoringMode.WinPoints;
            var (homeScore, awayScore, _) = RubberResolutionService.ComputeLeagueScore(
                allRubbers, fx.HomeTeamId.Value, fx.AwayTeamId.Value, scoringMode);

            bool alreadyFinalised = fx.Status == FixtureStatus.AwaitingVerification
                || fx.Status == FixtureStatus.Completed;

            fx.ResultSummary = $"{homeScore}-{awayScore}";

            // Persist fixture-level aggregates so downstream consumers (rating
            // replay, league table, result cards) don't have to re-walk the
            // rubber rows. Match FixtureSimulationService which already does this.
            fx.HomeSetsWon = allRubbers.Sum(r => r.HomeSetsWon ?? 0);
            fx.AwaySetsWon = allRubbers.Sum(r => r.AwaySetsWon ?? 0);
            fx.HomeGamesTotal = allRubbers.Sum(r => r.HomeGamesTotal ?? 0);
            fx.AwayGamesTotal = allRubbers.Sum(r => r.AwayGamesTotal ?? 0);

            // Winner is still determined by RUBBER count — that's what the team
            // league table uses for W/D/L (the LeagueScoring metric only drives
            // points). Keeping these aligned avoids "won the table row but lost
            // the fixture" weirdness.
            int? winner = homeRubbers > awayRubbers ? fx.HomeTeamId
                        : awayRubbers > homeRubbers ? fx.AwayTeamId
                        : null;

            if (!winner.HasValue && homeRubbers == awayRubbers)
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

    public async Task ClearRubberScoreAsync(int fixtureId, int rubberId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var rub = await db.Rubbers
            .Include(r => r.Fixture)
            .FirstOrDefaultAsync(r => r.CompetitionFixtureId == fixtureId && r.RubberId == rubberId, ct)
            ?? throw new KeyNotFoundException($"Rubber {rubberId} not found on fixture {fixtureId}.");

        // Reset the rubber itself.
        rub.IsComplete = false;
        rub.HomeSetsWon = null;
        rub.AwaySetsWon = null;
        rub.HomeGamesTotal = null;
        rub.AwayGamesTotal = null;
        rub.HomeGames = 0;
        rub.AwayGames = 0;
        rub.WinnerTeamId = null;
        rub.SavedMatchId = null;
        rub.SetScoresJson = null;

        // The parent fixture must be un-finalised — its aggregates and any
        // verification token now reflect a result that no longer holds.
        var fx = rub.Fixture;
        if (fx is not null)
        {
            fx.Status = FixtureStatus.Scheduled;
            fx.ResultSummary = null;
            fx.HomeSetsWon = null;
            fx.AwaySetsWon = null;
            fx.HomeGamesTotal = null;
            fx.AwayGamesTotal = null;
            fx.WinnerTeamId = null;
            fx.CompletedAt = null;
            fx.VerificationToken = null;
        }

        await db.SaveChangesAsync(ct);
    }

    private static CompetitionDto ToDto(Competition c) => new(
        c.CompetitionID, c.CompetitionName,
        c.CompetitionFormat,
        c.MaxParticipants, c.StartDate, c.EndDate, c.MaxAge, c.MinAge,
        c.EligibleSex,
        c.HostClubId, c.HostClub?.Name, c.RulesSetId, c.Rules?.Name,
        c.EventId, c.Event?.Name, c.IsArchived, c.IsStarted,
        c.CreatorUserId, c.IsRestricted, c.RegisterByDate, c.WizardStep);

    public async Task<MyCompetitionsViewDto> GetMyCompetitionsViewAsync(CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(currentUser.UserId))
            return new MyCompetitionsViewDto(false, [], [], null);

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
        if (myPlayers.Count == 0) return new MyCompetitionsViewDto(false, [], [], null);
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
        // SinglesLadder is a continuous format — players can register at any
        // point in the ladder's lifetime, so the IsStarted flag and the
        // start-date-in-the-past check (both "registration closed" gates for
        // bracket/league formats) don't apply. RegisterByDate still does:
        // admins keep the option of a hard registration cutoff.
        var candidates = await db.Competition
            .Include(c => c.HostClub)
            .Include(c => c.Event)
            .Include(c => c.AllowedPlayers)
            .Where(c => !enteredIds.Contains(c.CompetitionID) && !c.IsArchived
                        && (c.CompetitionFormat == CompetitionFormat.SinglesLadder || !c.IsStarted))
            .Where(c => c.CompetitionFormat == CompetitionFormat.SinglesLadder
                        || !c.StartDate.HasValue || c.StartDate.Value >= today)
            .Where(c => !c.RegisterByDate.HasValue || c.RegisterByDate.Value >= today)
            .OrderBy(c => c.StartDate).ThenBy(c => c.CompetitionName)
            .ToListAsync(ct);

        var available = candidates
            .Where(c => EligibilityEvaluator.Evaluate(c, eligibilityPlayer).Count == 0)
            .Select(ToDto).ToList();
        var myMobile = await EnsurePlayerMobileSyncedAsync(db, eligibilityPlayer, ct);
        return new MyCompetitionsViewDto(
            true,
            enteredEntities.Select(ToDto).ToList(),
            available,
            myMobile);
    }

    public async Task<List<CompetitionFixtureDto>> GetPendingVerificationFixturesAsync(CancellationToken ct = default)
    {
        var user = await GetPrincipalAsync();
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
        var user = await GetPrincipalAsync();
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
        var user = await GetPrincipalAsync();
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

        // PlayerRatingSnapshot.CompetitionId uses OnDelete(NoAction) to keep
        // the Elo audit trail intact under normal operation. When the user
        // explicitly deletes an (archived) competition, drop them too — the
        // alternative is the delete silently fails on a FK constraint.
        var parentSnapshots = await db.PlayerRatingSnapshots
            .Where(s => s.CompetitionId == competitionId)
            .ToListAsync(ct);

        // Children that reference this competition via SeededFromCompetitionId
        // must be cut loose so the self-referential FK doesn't block the delete.
        // For each child participant who hasn't already had their SeasonStart
        // baked in (via admin acceptance or manual entry), persist the rating
        // they would have inherited from this parent — same precedence as
        // PlayerRatingService.GetCurrentRatingAsync: parent SeasonEnd > parent
        // SeasonStart > nothing. The child then has self-contained snapshots
        // and we can null its SeededFromCompetitionId.
        var children = await db.Competition
            .Where(c => c.SeededFromCompetitionId == competitionId)
            .ToListAsync(ct);
        foreach (var child in children)
        {
            var childParticipantIds = await db.CompetitionParticipants
                .Where(p => p.CompetitionId == child.CompetitionID)
                .Select(p => p.PlayerId)
                .ToListAsync(ct);

            var alreadyBaked = await db.PlayerRatingSnapshots
                .Where(s => s.CompetitionId == child.CompetitionID
                         && s.Kind == PlayerRatingSnapshotKind.SeasonStart)
                .Select(s => s.PlayerId)
                .ToListAsync(ct);
            var alreadyBakedSet = alreadyBaked.ToHashSet();

            foreach (var playerId in childParticipantIds)
            {
                if (alreadyBakedSet.Contains(playerId)) continue;

                var forPlayer = parentSnapshots.Where(s => s.PlayerId == playerId).ToList();
                var source =
                    forPlayer.FirstOrDefault(s => s.Kind == PlayerRatingSnapshotKind.SeasonEnd)
                    ?? forPlayer.FirstOrDefault(s => s.Kind == PlayerRatingSnapshotKind.SeasonStart);
                if (source is null) continue;

                db.PlayerRatingSnapshots.Add(new PlayerRatingSnapshot
                {
                    PlayerId = playerId,
                    CompetitionId = child.CompetitionID,
                    Kind = PlayerRatingSnapshotKind.SeasonStart,
                    Rating = source.Rating,
                    RubbersPlayed = 0,
                    IsProvisional = source.IsProvisional,
                    ComputedAt = DateTime.UtcNow,
                });
            }

            child.SeededFromCompetitionId = null;
        }

        db.PlayerRatingSnapshots.RemoveRange(parentSnapshots);

        db.Competition.Remove(entity);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex)
        {
            logger.LogError(ex, "Failed to delete competition {CompetitionId}", competitionId);
            // The outer DbUpdateException message is the generic EF wrapper
            // ("An error occurred while saving the entity changes…"); the
            // inner SqlException carries the actual FK constraint name. Rethrow
            // as InvalidOperationException so the UI can surface ex.Message
            // without needing an EF Core reference.
            var detail = ex.InnerException?.Message ?? ex.Message;
            throw new InvalidOperationException(detail, ex);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to delete competition {CompetitionId}", competitionId);
            throw;
        }
    }

    public async Task EnterCompetitionAsync(
        int competitionId,
        PhoneShareConsent consent,
        ParticipantStatus status = ParticipantStatus.FullPlayer,
        CancellationToken ct = default)
    {
        if (status is not (ParticipantStatus.FullPlayer or ParticipantStatus.Substitute))
            throw new InvalidOperationException(
                "Choose to enter as a full player or as a substitute.");

        if (string.IsNullOrEmpty(currentUser.UserId))
            throw new InvalidOperationException("Not authenticated.");

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var player = await db.Players
            .FirstOrDefaultAsync(p => p.UserId == currentUser.UserId && !p.IsLight, ct)
            ?? throw new InvalidOperationException("You need a player profile before entering competitions.");

        await EnsurePlayerMobileSyncedAsync(db, player, ct);
        RequirePhoneAndConsent(player, consent);

        var comp = await db.Competition
            .Include(c => c.AllowedPlayers)
            .FirstOrDefaultAsync(c => c.CompetitionID == competitionId, ct)
            ?? throw new KeyNotFoundException($"Competition {competitionId} not found.");

        var today = DateTime.UtcNow.Date;
        // SinglesLadder is continuous — players can join at any point, so
        // the "already started" gate (both the explicit IsStarted flag and
        // a past StartDate) doesn't apply. RegisterByDate still does.
        var isLadder = comp.CompetitionFormat == CompetitionFormat.SinglesLadder;
        if (!isLadder && (comp.IsStarted || (comp.StartDate.HasValue && comp.StartDate.Value < today)))
            throw new InvalidOperationException("This competition has already started.");
        if (comp.RegisterByDate.HasValue && comp.RegisterByDate.Value < today)
            throw new InvalidOperationException("Registration for this competition has closed.");

        var violations = EligibilityEvaluator.Evaluate(comp, player);
        if (violations.Count > 0)
            throw new InvalidOperationException($"You're not eligible for this competition: {violations[0].Message}");

        // MaxParticipants caps the full roster only — substitutes don't count
        // against it (they're explicitly extra coverage).
        if (status == ParticipantStatus.FullPlayer && comp.MaxParticipants.HasValue)
        {
            var currentCount = await db.CompetitionParticipants
                .CountAsync(cp => cp.CompetitionId == competitionId
                    && cp.Status == ParticipantStatus.FullPlayer, ct);
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
            Status = status
        });
        db.CompetitionEntryConsents.Add(BuildConsentRow(competitionId, player.PlayerId, consent));
        await db.SaveChangesAsync(ct);
    }

    public async Task LeaveCompetitionAsync(int competitionId, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(currentUser.UserId))
            throw new InvalidOperationException("Not authenticated.");

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var participant = await db.CompetitionParticipants
            .Include(cp => cp.Player)
            .FirstOrDefaultAsync(cp => cp.CompetitionId == competitionId
                && cp.Player!.UserId == currentUser.UserId, ct)
            ?? throw new KeyNotFoundException("You are not entered in this competition.");

        participant.Status = ParticipantStatus.Withdrawn;

        // Stamp the latest active consent row (if any) as withdrawn so the
        // visibility service drops this player's number from peer views.
        var activeConsent = await db.CompetitionEntryConsents
            .Where(c => c.CompetitionId == competitionId
                        && c.PlayerId == participant.PlayerId
                        && c.WithdrawnUtc == null)
            .OrderByDescending(c => c.ConsentGivenUtc)
            .FirstOrDefaultAsync(ct);
        if (activeConsent is not null)
            activeConsent.WithdrawnUtc = DateTime.UtcNow;

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
