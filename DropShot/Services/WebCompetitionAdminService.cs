using System.Security.Claims;
using DropShot.Data;
using DropShot.Models;
using DropShot.Shared;
using DropShot.Shared.Dtos;
using DropShot.UI.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace DropShot.Services;

public sealed class WebCompetitionAdminService(
    IDbContextFactory<MyDbContext> dbFactory,
    ClubAuthorizationService authzService,
    IHttpContextAccessor httpContextAccessor,
    AuthenticationStateProvider authStateProvider,
    UserManager<ApplicationUser> userManager,
    ICompetitionRubberTemplateProvider rubberTemplateProvider,
    AdminEmailService adminEmailService,
    CompetitionSchedulerService scheduler,
    FixtureSimulationService fixtureSimulator) : ICompetitionAdminService
{
    // IHttpContextAccessor.HttpContext is null in interactive Blazor Server mode (SignalR
    // circuit). Fall back to AuthenticationStateProvider, which works in both SSR and
    // interactive phases. The HttpContext path is kept for API/MAUI JWT bearer requests
    // where there is no Blazor circuit.
    private async Task<ClaimsPrincipal> GetCurrentPrincipalAsync()
    {
        var ctx = httpContextAccessor.HttpContext;
        if (ctx?.User?.Identity?.IsAuthenticated == true) return ctx.User;
        try
        {
            var state = await authStateProvider.GetAuthenticationStateAsync();
            return state.User;
        }
        catch
        {
            return new ClaimsPrincipal();
        }
    }

    // ── Read ─────────────────────────────────────────────────────────────────

    public async Task<CompetitionEditDto?> GetCompetitionForEditAsync(int? competitionId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var clubs = await db.Clubs.AsNoTracking().OrderBy(c => c.Name)
            .Select(c => new ClubDto(c.ClubId, c.Name, c.AddressLine1, c.AddressLine2, c.Town, c.Postcode,
                c.Phone, c.Email, c.Website, c.Courts.Count))
            .ToListAsync(ct);
        var rulesSets = await db.RulesSets.AsNoTracking().OrderBy(r => r.Name)
            .Select(r => new RulesSetDto(r.RulesSetId, r.Name, r.Description, r.Items.Count, r.ClubId))
            .ToListAsync(ct);
        var events = await db.Events.AsNoTracking().OrderBy(e => e.Name)
            .Select(e => new EventDto(e.EventId, e.Name, e.Description, e.StartDate, e.EndDate,
                e.HostClubId, e.HostClub != null ? e.HostClub.Name : null,
                e.Competitions.Count))
            .ToListAsync(ct);

        if (!competitionId.HasValue || competitionId.Value <= 0)
        {
            // Create-mode payload: lookups + authz flags, no entity data.
            var canCreate = await CanEditCompetitionAsync(null, ct);
            var isSuperAdminCreate = authzService.IsSuperAdmin(await GetCurrentPrincipalAsync());
            return new CompetitionEditDto(
                CompetitionId: null,
                CompetitionName: "",
                CompetitionFormat: null,
                MaxParticipants: null,
                StartDate: null, EndDate: null, RegisterByDate: null,
                MaxAge: null, MinAge: null, EligibleSex: null,
                RulesSetId: null, HostClubId: null, HostClubName: null,
                EventId: null, EventName: null,
                BestOf: 3, RequireVerification: false, IsArchived: false, IsStarted: false,
                CreatorUserId: null, IsRestricted: false,
                TeamSize: null, RubberTemplateKey: null,
                MatchFormat: MatchFormatType.BestOf, NumberOfSets: 3, GamesPerSet: 6,
                SetWinMode: SetWinMode.WinBy2, LeagueScoring: LeagueScoringMode.WinPoints,
                RubberTieBreak: RubberTieBreakMode.AdminDecides,
                MinDaysBetweenPlayerMatches: null, HasDivisions: false,
                SeededFromCompetitionId: null,
                Stages: [], Participants: [], Divisions: [], Teams: [], Fixtures: [],
                MatchWindows: [], CourtPairs: [], Admins: [],
                RubberTemplate: null,
                SeedSourceCandidates: [],
                ClubTemplates: [], CompetitionTemplates: [], EmailTemplates: [],
                HostClubCourts: [],
                Clubs: clubs, RulesSets: rulesSets, Events: events,
                AllowedPlayerIds: [],
                CanEdit: canCreate,
                IsSuperAdmin: isSuperAdminCreate);
        }

        var id = competitionId.Value;
        var comp = await db.Competition
            .Include(c => c.HostClub)
            .Include(c => c.Rules)
            .Include(c => c.Event)
            .Include(c => c.Stages.OrderBy(s => s.StageOrder))
            .Include(c => c.Participants).ThenInclude(p => p.Player)
            .Include(c => c.Participants).ThenInclude(p => p.Team)
            .Include(c => c.Participants).ThenInclude(p => p.Division)
            .Include(c => c.Fixtures).ThenInclude(f => f.Stage)
            .Include(c => c.Fixtures).ThenInclude(f => f.Court)
            .Include(c => c.Fixtures).ThenInclude(f => f.Player1)
            .Include(c => c.Fixtures).ThenInclude(f => f.Player2)
            .Include(c => c.Fixtures).ThenInclude(f => f.Player3)
            .Include(c => c.Fixtures).ThenInclude(f => f.Player4)
            .Include(c => c.Fixtures).ThenInclude(f => f.HomeTeam)
            .Include(c => c.Fixtures).ThenInclude(f => f.AwayTeam)
            .Include(c => c.Fixtures).ThenInclude(f => f.CourtPair).ThenInclude(cp => cp!.Court1)
            .Include(c => c.Fixtures).ThenInclude(f => f.CourtPair).ThenInclude(cp => cp!.Court2)
            .Include(c => c.Teams).ThenInclude(t => t.Captain)
            .Include(c => c.MatchWindows).ThenInclude(w => w.Court)
            .Include(c => c.MatchWindows).ThenInclude(w => w.Division)
            .Include(c => c.Divisions)
            .Include(c => c.AllowedPlayers)
            .AsSplitQuery()
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.CompetitionID == id, ct);

        if (comp is null) return null;

        if (!await authzService.CanEditCompetitionAsync(await GetCurrentPrincipalAsync(), comp.HostClubId, id))
            return null;

        var canEdit = true;
        var isSuperAdmin = authzService.IsSuperAdmin(await GetCurrentPrincipalAsync());

        var courtPairs = await db.CourtPairs.AsNoTracking()
            .Where(cp => cp.CompetitionId == id)
            .Include(cp => cp.Court1).Include(cp => cp.Court2)
            .OrderBy(cp => cp.Name)
            .Select(cp => new CourtPairDto(
                cp.CourtPairId, cp.CompetitionId,
                cp.Court1Id, cp.Court1!.Name, cp.Court2Id, cp.Court2!.Name, cp.Name))
            .ToListAsync(ct);

        var seedCandidates = await db.Competition.AsNoTracking()
            .Where(c => c.CompetitionID != id
                        && c.CompetitionFormat == comp.CompetitionFormat
                        && c.HostClubId == comp.HostClubId
                        && c.Divisions.Any())
            .OrderByDescending(c => c.StartDate)
            .ThenBy(c => c.CompetitionName)
            .Select(c => new CompetitionSeedSourceDto(
                c.CompetitionID, c.CompetitionName, c.EndDate, c.Divisions.Count))
            .ToListAsync(ct);

        var hostClubCourts = new List<CourtDto>();
        var clubTemplates = new List<ClubSchedulingTemplateDto>();
        var competitionTemplates = new List<CompetitionTemplateDto>();
        var emailTemplates = new List<ClubEmailTemplateDto>();
        if (comp.HostClubId.HasValue)
        {
            hostClubCourts = await db.Courts.AsNoTracking()
                .Where(c => c.ClubId == comp.HostClubId.Value)
                .OrderBy(c => c.Name)
                .Select(c => new CourtDto(c.CourtId, c.ClubId, c.Name, (DropShot.Shared.CourtSurface)c.Surface, c.IsIndoor))
                .ToListAsync(ct);

            clubTemplates = await db.ClubSchedulingTemplates.AsNoTracking()
                .Include(t => t.Windows)
                .Where(t => t.ClubId == comp.HostClubId.Value)
                .OrderBy(t => t.Name)
                .Select(t => new ClubSchedulingTemplateDto(
                    t.ClubSchedulingTemplateId, t.ClubId, t.Name,
                    t.Windows.Select(w => new ClubSchedulingTemplateWindowDto(
                        w.ClubSchedulingTemplateWindowId, w.DayOfWeek, w.StartTime, w.EndTime)).ToList()))
                .ToListAsync(ct);

            competitionTemplates = await db.CompetitionTemplates.AsNoTracking()
                .Include(t => t.Windows)
                .Where(t => t.ClubId == comp.HostClubId.Value)
                .OrderBy(t => t.Name)
                .Select(t => new CompetitionTemplateDto(
                    t.CompetitionTemplateId, t.ClubId, t.Name, t.Format, t.RulesSetId, t.BestOf,
                    t.MaxAge, t.EligibleSex,
                    t.Windows.Select(w => new CompetitionTemplateWindowDto(
                        w.CompetitionTemplateWindowId, w.DayOfWeek, w.StartTime, w.EndTime)).ToList()))
                .ToListAsync(ct);

            emailTemplates = await db.ClubEmailTemplates.AsNoTracking()
                .Where(t => t.ClubId == comp.HostClubId.Value)
                .OrderBy(t => t.Name)
                .Select(t => new ClubEmailTemplateDto(
                    t.ClubEmailTemplateId, t.ClubId, t.Name, t.Subject, t.Body))
                .ToListAsync(ct);
        }

        var admins = await GetCompetitionAdminsInternalAsync(db, id, ct);
        var rubberTemplate = await LoadRubberTemplateStateInternalAsync(db, comp, ct);

        return new CompetitionEditDto(
            CompetitionId: comp.CompetitionID,
            CompetitionName: comp.CompetitionName,
            CompetitionFormat: comp.CompetitionFormat,
            MaxParticipants: comp.MaxParticipants,
            StartDate: comp.StartDate, EndDate: comp.EndDate, RegisterByDate: comp.RegisterByDate,
            MaxAge: comp.MaxAge, MinAge: comp.MinAge, EligibleSex: comp.EligibleSex,
            RulesSetId: comp.RulesSetId, HostClubId: comp.HostClubId, HostClubName: comp.HostClub?.Name,
            EventId: comp.EventId, EventName: comp.Event?.Name,
            BestOf: comp.BestOf, RequireVerification: comp.RequireVerification,
            IsArchived: comp.IsArchived, IsStarted: comp.IsStarted,
            CreatorUserId: comp.CreatorUserId, IsRestricted: comp.IsRestricted,
            TeamSize: comp.TeamSize, RubberTemplateKey: comp.RubberTemplateKey,
            MatchFormat: comp.MatchFormat, NumberOfSets: comp.NumberOfSets, GamesPerSet: comp.GamesPerSet,
            SetWinMode: comp.SetWinMode, LeagueScoring: comp.LeagueScoring,
            RubberTieBreak: comp.RubberTieBreak,
            MinDaysBetweenPlayerMatches: comp.MinDaysBetweenPlayerMatches,
            HasDivisions: comp.HasDivisions,
            SeededFromCompetitionId: comp.SeededFromCompetitionId,
            Stages: comp.Stages.Select(ToStageDto).ToList(),
            Participants: comp.Participants.Select(ToParticipantDto).ToList(),
            Divisions: comp.Divisions.OrderBy(d => d.Rank).Select(ToDivisionDto).ToList(),
            Teams: comp.Teams.OrderBy(t => t.Name).Select(ToTeamDto).ToList(),
            Fixtures: comp.Fixtures
                .OrderBy(f => f.RoundNumber).ThenBy(f => f.ScheduledAt)
                .Select(ToFixtureDto).ToList(),
            MatchWindows: comp.MatchWindows
                .OrderBy(w => w.DayOfWeek).ThenBy(w => w.StartTime)
                .Select(ToMatchWindowDto).ToList(),
            CourtPairs: courtPairs,
            Admins: admins,
            RubberTemplate: rubberTemplate,
            SeedSourceCandidates: seedCandidates,
            ClubTemplates: clubTemplates,
            CompetitionTemplates: competitionTemplates,
            EmailTemplates: emailTemplates,
            HostClubCourts: hostClubCourts,
            Clubs: clubs, RulesSets: rulesSets, Events: events,
            AllowedPlayerIds: comp.AllowedPlayers.Select(ap => ap.PlayerId).ToList(),
            CanEdit: canEdit,
            IsSuperAdmin: isSuperAdmin);
    }

    public async Task<List<CompetitionSeedSourceDto>> GetSeedSourceCandidatesAsync(
        int? excludeCompetitionId, CompetitionFormat format, int? hostClubId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.Competition.AsNoTracking()
            .Where(c => (!excludeCompetitionId.HasValue || c.CompetitionID != excludeCompetitionId.Value)
                        && c.CompetitionFormat == format
                        && c.HostClubId == hostClubId
                        && c.Divisions.Any())
            .OrderByDescending(c => c.StartDate)
            .ThenBy(c => c.CompetitionName)
            .Select(c => new CompetitionSeedSourceDto(
                c.CompetitionID, c.CompetitionName, c.EndDate, c.Divisions.Count))
            .ToListAsync(ct);
    }

    public async Task<List<ClubSchedulingTemplateDto>> GetClubTemplatesAsync(int clubId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.ClubSchedulingTemplates.AsNoTracking()
            .Include(t => t.Windows)
            .Where(t => t.ClubId == clubId)
            .OrderBy(t => t.Name)
            .Select(t => new ClubSchedulingTemplateDto(
                t.ClubSchedulingTemplateId, t.ClubId, t.Name,
                t.Windows.Select(w => new ClubSchedulingTemplateWindowDto(
                    w.ClubSchedulingTemplateWindowId, w.DayOfWeek, w.StartTime, w.EndTime)).ToList()))
            .ToListAsync(ct);
    }

    public async Task<List<ClubEmailTemplateDto>> GetEmailTemplatesAsync(int clubId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.ClubEmailTemplates.AsNoTracking()
            .Where(t => t.ClubId == clubId)
            .OrderBy(t => t.Name)
            .Select(t => new ClubEmailTemplateDto(
                t.ClubEmailTemplateId, t.ClubId, t.Name, t.Subject, t.Body))
            .ToListAsync(ct);
    }

    public async Task<bool> CanEditCompetitionAsync(int? competitionId, CancellationToken ct = default)
    {
        if (competitionId.HasValue && competitionId.Value > 0)
        {
            await using var db = await dbFactory.CreateDbContextAsync(ct);
            var hostClubId = await db.Competition.AsNoTracking()
                .Where(c => c.CompetitionID == competitionId.Value)
                .Select(c => c.HostClubId)
                .FirstOrDefaultAsync(ct);
            return await authzService.CanEditCompetitionAsync(await GetCurrentPrincipalAsync(), hostClubId, competitionId.Value);
        }

        var principal = await GetCurrentPrincipalAsync();
        var appUser = await userManager.GetUserAsync(principal);
        var canCreateUserComp = authzService.CanCreateUserCompetition(principal, appUser);
        var isAdmin = await authzService.IsAdminAsync(principal);
        var adminClubIds = await authzService.GetAdminClubIdsAsync(principal);
        return isAdmin || adminClubIds.Count > 0 || canCreateUserComp;
    }

    public async Task<bool> IsSuperAdminAsync(CancellationToken ct = default)
        => authzService.IsSuperAdmin(await GetCurrentPrincipalAsync());

    public async Task<List<CompetitionAdminRowDto>> GetCompetitionAdminsAsync(int competitionId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await GetCompetitionAdminsInternalAsync(db, competitionId, ct);
    }

    private static Task<List<CompetitionAdminRowDto>> GetCompetitionAdminsInternalAsync(
        MyDbContext db, int competitionId, CancellationToken ct)
        => db.CompetitionAdmins.AsNoTracking()
            .Where(ca => ca.CompetitionId == competitionId)
            .Join(db.Users, ca => ca.UserId, u => u.Id,
                  (ca, u) => new CompetitionAdminRowDto(ca.UserId, u.Email ?? "", ca.AssignedAt))
            .ToListAsync(ct);

    // ── Competition lifecycle ────────────────────────────────────────────────

    public async Task<int> SaveCompetitionAsync(
        int? competitionId, SaveCompetitionEditRequest request, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var name = request.CompetitionName.Trim();

        var nameExists = await db.Competition.AsNoTracking()
            .AnyAsync(c => (!competitionId.HasValue || c.CompetitionID != competitionId.Value)
                           && c.CompetitionName.Trim().ToLower() == name.ToLower(), ct);
        if (nameExists)
            throw new InvalidOperationException($"A competition named \"{name}\" already exists.");

        if (competitionId.HasValue && competitionId.Value > 0)
        {
            var comp = await db.Competition.FirstOrDefaultAsync(c => c.CompetitionID == competitionId.Value, ct)
                ?? throw new KeyNotFoundException("Competition not found.");

            if (!await authzService.CanEditCompetitionAsync(await GetCurrentPrincipalAsync(), comp.HostClubId, comp.CompetitionID))
                throw new UnauthorizedAccessException("You can't edit this competition.");

            ApplyRequestToEntity(comp, request, name);
            await db.SaveChangesAsync(ct);
            return comp.CompetitionID;
        }
        else
        {
            if (!await CanEditCompetitionAsync(null, ct))
                throw new UnauthorizedAccessException("You can't create a competition.");

            var comp = new Competition();
            ApplyRequestToEntity(comp, request, name);
            if (comp.HostClubId is null)
                comp.CreatorUserId = userManager.GetUserId(await GetCurrentPrincipalAsync());
            else
                comp.CreatorUserId = null;

            db.Competition.Add(comp);
            await db.SaveChangesAsync(ct);
            return comp.CompetitionID;
        }
    }

    private static void ApplyRequestToEntity(Competition comp, SaveCompetitionEditRequest req, string name)
    {
        comp.CompetitionName = name;
        comp.CompetitionFormat = req.CompetitionFormat;
        comp.EligibleSex = req.EligibleSex;
        comp.MaxParticipants = req.MaxParticipants;
        comp.StartDate = req.StartDate;
        comp.EndDate = req.EndDate;
        comp.RegisterByDate = req.RegisterByDate;
        comp.MaxAge = req.MaxAge;
        comp.MinAge = req.MinAge;
        comp.HostClubId = req.HostClubId;
        comp.RulesSetId = req.RulesSetId;
        comp.EventId = req.EventId;
        comp.BestOf = req.BestOf;
        comp.RequireVerification = req.RequireVerification;
        comp.TeamSize = req.TeamSize;
        comp.RubberTemplateKey = req.RubberTemplateKey;
        comp.MatchFormat = req.MatchFormat;
        comp.NumberOfSets = req.NumberOfSets;
        comp.GamesPerSet = req.GamesPerSet;
        comp.SetWinMode = req.SetWinMode;
        comp.LeagueScoring = req.LeagueScoring;
        comp.RubberTieBreak = req.RubberTieBreak;
        comp.MinDaysBetweenPlayerMatches = req.MinDaysBetweenPlayerMatches;
        comp.HasDivisions = req.HasDivisions;
        comp.SeededFromCompetitionId = req.SeededFromCompetitionId;
        comp.IsRestricted = req.IsRestricted;
    }

    public async Task<CloneCompetitionResultDto> CloneCompetitionAsync(
        int sourceCompetitionId, CloneCompetitionRequest request, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var source = await db.Competition
            .Include(c => c.Stages)
            .Include(c => c.RubberTemplate!).ThenInclude(rt => rt.Rubbers)
            .FirstOrDefaultAsync(c => c.CompetitionID == sourceCompetitionId, ct)
            ?? throw new KeyNotFoundException("Source competition not found.");

        if (!await authzService.CanEditCompetitionAsync(await GetCurrentPrincipalAsync(), source.HostClubId, sourceCompetitionId))
            throw new UnauthorizedAccessException("You can't clone this competition.");

        var newComp = new Competition
        {
            CompetitionName             = request.NewName.Trim(),
            CompetitionFormat           = source.CompetitionFormat,
            HostClubId                  = source.HostClubId,
            CreatorUserId               = source.HostClubId == null ? source.CreatorUserId : null,
            MaxParticipants             = source.MaxParticipants,
            MaxAge                      = source.MaxAge,
            MinAge                      = source.MinAge,
            EligibleSex                 = source.EligibleSex,
            RulesSetId                  = source.RulesSetId,
            EventId                     = source.EventId,
            HasDivisions                = source.HasDivisions,
            BestOf                      = source.BestOf,
            MatchFormat                 = source.MatchFormat,
            NumberOfSets                = source.NumberOfSets,
            GamesPerSet                 = source.GamesPerSet,
            SetWinMode                  = source.SetWinMode,
            LeagueScoring               = source.LeagueScoring,
            RubberTieBreak              = source.RubberTieBreak,
            MinDaysBetweenPlayerMatches = source.MinDaysBetweenPlayerMatches,
            TeamSize                    = source.TeamSize,
            RubberTemplateKey           = source.RubberTemplateKey,
            RequireVerification         = source.RequireVerification,
            IsRestricted                = source.IsRestricted,
            SeededFromCompetitionId     = sourceCompetitionId,
        };
        db.Competition.Add(newComp);
        await db.SaveChangesAsync(ct);

        foreach (var stage in source.Stages.OrderBy(s => s.StageOrder))
        {
            db.CompetitionStages.Add(new CompetitionStage
            {
                CompetitionId = newComp.CompetitionID,
                Name          = stage.Name,
                StageOrder    = stage.StageOrder,
                StageType     = stage.StageType,
            });
        }

        if (source.RubberTemplate?.Rubbers.Count > 0)
        {
            var newTemplate = new CompetitionRubberTemplate { CompetitionId = newComp.CompetitionID };
            db.CompetitionRubberTemplates.Add(newTemplate);
            await db.SaveChangesAsync(ct);
            foreach (var r in source.RubberTemplate.Rubbers.OrderBy(r => r.Order))
            {
                db.RubberTemplateRubbers.Add(new RubberTemplateRubber
                {
                    CompetitionRubberTemplateId = newTemplate.CompetitionRubberTemplateId,
                    Order         = r.Order,
                    Name          = r.Name,
                    CourtNumber   = r.CourtNumber,
                    HomeRolesJson = r.HomeRolesJson,
                    AwayRolesJson = r.AwayRolesJson,
                });
            }
        }

        if (request.CopyParticipants)
        {
            var toClone = await db.CompetitionParticipants
                .Where(p => p.CompetitionId == sourceCompetitionId
                            && p.Status != ParticipantStatus.Withdrawn
                            && p.Status != ParticipantStatus.Disqualified)
                .ToListAsync(ct);
            foreach (var p in toClone)
            {
                db.CompetitionParticipants.Add(new CompetitionParticipant
                {
                    CompetitionId = newComp.CompetitionID,
                    PlayerId      = p.PlayerId,
                    RegisteredAt  = DateTime.UtcNow,
                    Status        = ParticipantStatus.Registered,
                });
            }
        }

        await db.SaveChangesAsync(ct);
        return new CloneCompetitionResultDto(newComp.CompetitionID);
    }

    public async Task<bool> ToggleStartedAsync(int competitionId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var comp = await db.Competition.FindAsync([competitionId], ct)
            ?? throw new KeyNotFoundException("Competition not found.");

        if (!await authzService.CanEditCompetitionAsync(await GetCurrentPrincipalAsync(), comp.HostClubId, competitionId))
            throw new UnauthorizedAccessException("You can't edit this competition.");

        comp.IsStarted = !comp.IsStarted;
        await db.SaveChangesAsync(ct);
        return comp.IsStarted;
    }

    // ── Stages ───────────────────────────────────────────────────────────────

    public async Task ApplyStageFollowUpAsync(
        int competitionId, ApplyStageFollowUpRequest request, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var comp = await db.Competition
            .Include(c => c.Stages)
            .FirstOrDefaultAsync(c => c.CompetitionID == competitionId, ct)
            ?? throw new KeyNotFoundException("Competition not found.");

        if (!await authzService.CanEditCompetitionAsync(await GetCurrentPrincipalAsync(), comp.HostClubId, competitionId))
            throw new UnauthorizedAccessException("You can't edit this competition.");

        foreach (var type in request.StageTypes)
        {
            db.CompetitionStages.Add(new CompetitionStage
            {
                CompetitionId = competitionId,
                Name          = StageDisplayName(type),
                StageOrder    = 0,
                StageType     = type,
            });
        }
        await db.SaveChangesAsync(ct);

        var stages = await db.CompetitionStages
            .Where(s => s.CompetitionId == competitionId)
            .ToListAsync(ct);
        var ordered = stages.OrderBy(s => StageTypeSortKey(s.StageType)).ToList();
        for (int i = 0; i < ordered.Count; i++) ordered[i].StageOrder = i;
        await db.SaveChangesAsync(ct);
    }

    // ── Admins ───────────────────────────────────────────────────────────────

    public async Task AddCompetitionAdminAsync(
        int competitionId, AddCompetitionAdminRequest request, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var comp = await db.Competition.AsNoTracking()
            .FirstOrDefaultAsync(c => c.CompetitionID == competitionId, ct)
            ?? throw new KeyNotFoundException("Competition not found.");
        if (!await authzService.CanEditCompetitionAsync(await GetCurrentPrincipalAsync(), comp.HostClubId, competitionId))
            throw new UnauthorizedAccessException("You can't manage admins for this competition.");

        var email = request.Email.Trim();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email, ct)
            ?? throw new KeyNotFoundException("No user with that email.");
        var alreadyAdmin = await db.CompetitionAdmins
            .AnyAsync(ca => ca.CompetitionId == competitionId && ca.UserId == user.Id, ct);
        if (alreadyAdmin)
            throw new InvalidOperationException("That user is already an admin of this competition.");

        db.CompetitionAdmins.Add(new CompetitionAdmin
        {
            CompetitionId = competitionId,
            UserId        = user.Id,
            AssignedAt    = DateTime.UtcNow,
        });
        await db.SaveChangesAsync(ct);
    }

    public async Task RemoveCompetitionAdminAsync(int competitionId, string userId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var comp = await db.Competition.AsNoTracking()
            .FirstOrDefaultAsync(c => c.CompetitionID == competitionId, ct)
            ?? throw new KeyNotFoundException("Competition not found.");
        if (!await authzService.CanEditCompetitionAsync(await GetCurrentPrincipalAsync(), comp.HostClubId, competitionId))
            throw new UnauthorizedAccessException("You can't manage admins for this competition.");

        var ca = await db.CompetitionAdmins.FindAsync([competitionId, userId], ct);
        if (ca is null) return;
        db.CompetitionAdmins.Remove(ca);
        await db.SaveChangesAsync(ct);
    }

    // ── Rubber template ──────────────────────────────────────────────────────

    public async Task<RubberTemplateStateDto> LoadRubberTemplateStateAsync(int competitionId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var comp = await db.Competition.AsNoTracking()
            .Include(c => c.RubberTemplate).ThenInclude(t => t!.Rubbers)
            .FirstOrDefaultAsync(c => c.CompetitionID == competitionId, ct)
            ?? throw new KeyNotFoundException("Competition not found.");
        return await LoadRubberTemplateStateInternalAsync(db, comp, ct);
    }

    private async Task<RubberTemplateStateDto> LoadRubberTemplateStateInternalAsync(
        MyDbContext db, Competition comp, CancellationToken ct)
    {
        string source;
        string? selectedKey;
        if (comp.RubberTemplate is { Rubbers.Count: > 0 })
        {
            source = "custom";
            selectedKey = null;
        }
        else if (!string.IsNullOrEmpty(comp.RubberTemplateKey))
        {
            source = "preset";
            selectedKey = comp.RubberTemplateKey;
        }
        else
        {
            source = "default";
            selectedKey = null;
        }

        var template = await rubberTemplateProvider.GetAsync(db, comp.CompetitionID);
        var defs = template?.ToList() ?? [];
        var roles = RubberTemplateRegistry.GetRoleSet(template).ToList();
        var presets = RubberTemplateRegistry.AvailablePresets()
            .Select(p => new RubberPresetDto(p.Key, p.Label))
            .ToList();
        return new RubberTemplateStateDto(source, selectedKey, defs, presets, roles);
    }

    public async Task ApplyRubberPresetAsync(
        int competitionId, ApplyRubberPresetRequest request, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var comp = await db.Competition
            .FirstOrDefaultAsync(c => c.CompetitionID == competitionId, ct)
            ?? throw new KeyNotFoundException("Competition not found.");
        if (!await authzService.CanEditCompetitionAsync(await GetCurrentPrincipalAsync(), comp.HostClubId, competitionId))
            throw new UnauthorizedAccessException("You can't edit this competition.");

        comp.RubberTemplateKey = string.IsNullOrWhiteSpace(request.PresetKey) ? null : request.PresetKey;
        var custom = await db.CompetitionRubberTemplates
            .FirstOrDefaultAsync(t => t.CompetitionId == competitionId, ct);
        if (custom is not null) db.CompetitionRubberTemplates.Remove(custom);
        await db.SaveChangesAsync(ct);
    }

    public async Task ConvertToCustomTemplateAsync(int competitionId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var comp = await db.Competition
            .Include(c => c.RubberTemplate).ThenInclude(t => t!.Rubbers)
            .FirstOrDefaultAsync(c => c.CompetitionID == competitionId, ct)
            ?? throw new KeyNotFoundException("Competition not found.");
        if (!await authzService.CanEditCompetitionAsync(await GetCurrentPrincipalAsync(), comp.HostClubId, competitionId))
            throw new UnauthorizedAccessException("You can't edit this competition.");

        var resolved = await rubberTemplateProvider.GetAsync(db, competitionId);
        if (resolved is null || resolved.Count == 0) return;

        if (comp.RubberTemplate is null)
        {
            comp.RubberTemplate = new CompetitionRubberTemplate { CompetitionId = competitionId };
            db.CompetitionRubberTemplates.Add(comp.RubberTemplate);
        }
        else
        {
            db.RubberTemplateRubbers.RemoveRange(comp.RubberTemplate.Rubbers);
            comp.RubberTemplate.Rubbers.Clear();
        }

        int order = 1;
        foreach (var def in resolved)
        {
            comp.RubberTemplate.Rubbers.Add(new RubberTemplateRubber
            {
                Order       = order++,
                Name        = def.Name,
                CourtNumber = def.CourtNumber,
                HomeRoles   = def.HomeRoles.ToList(),
                AwayRoles   = def.AwayRoles.ToList(),
            });
        }
        await db.SaveChangesAsync(ct);
    }

    public async Task AddCustomRubberRowAsync(int competitionId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var comp = await db.Competition.AsNoTracking()
            .FirstOrDefaultAsync(c => c.CompetitionID == competitionId, ct)
            ?? throw new KeyNotFoundException("Competition not found.");
        if (!await authzService.CanEditCompetitionAsync(await GetCurrentPrincipalAsync(), comp.HostClubId, competitionId))
            throw new UnauthorizedAccessException("You can't edit this competition.");

        var template = await db.CompetitionRubberTemplates
            .Include(t => t.Rubbers)
            .FirstOrDefaultAsync(t => t.CompetitionId == competitionId, ct)
            ?? throw new InvalidOperationException("No custom template — convert first.");

        int nextOrder = template.Rubbers.Count == 0 ? 1 : template.Rubbers.Max(r => r.Order) + 1;
        template.Rubbers.Add(new RubberTemplateRubber
        {
            Order       = nextOrder,
            Name        = "Rubber " + nextOrder,
            CourtNumber = 1,
            HomeRoles   = ["A"],
            AwayRoles   = ["A"],
        });
        await db.SaveChangesAsync(ct);
    }

    public async Task SaveRubberRowAsync(
        int competitionId, SaveRubberRowRequest request, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var comp = await db.Competition.AsNoTracking()
            .FirstOrDefaultAsync(c => c.CompetitionID == competitionId, ct)
            ?? throw new KeyNotFoundException("Competition not found.");
        if (!await authzService.CanEditCompetitionAsync(await GetCurrentPrincipalAsync(), comp.HostClubId, competitionId))
            throw new UnauthorizedAccessException("You can't edit this competition.");

        var row = await db.RubberTemplateRubbers
            .Include(r => r.Template)
            .FirstOrDefaultAsync(r =>
                r.Template.CompetitionId == competitionId && r.Order == request.Order, ct)
            ?? throw new KeyNotFoundException("Rubber row not found.");

        row.Name        = string.IsNullOrWhiteSpace(request.Name) ? row.Name : request.Name.Trim();
        row.CourtNumber = request.CourtNumber < 1 ? 1 : request.CourtNumber;
        row.HomeRoles   = request.HomeRoles.ToList();
        row.AwayRoles   = request.AwayRoles.ToList();
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteCustomRubberRowAsync(int competitionId, int order, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var comp = await db.Competition.AsNoTracking()
            .FirstOrDefaultAsync(c => c.CompetitionID == competitionId, ct)
            ?? throw new KeyNotFoundException("Competition not found.");
        if (!await authzService.CanEditCompetitionAsync(await GetCurrentPrincipalAsync(), comp.HostClubId, competitionId))
            throw new UnauthorizedAccessException("You can't edit this competition.");

        var row = await db.RubberTemplateRubbers
            .Include(r => r.Template)
            .FirstOrDefaultAsync(r => r.Template.CompetitionId == competitionId && r.Order == order, ct);
        if (row is null) return;
        db.RubberTemplateRubbers.Remove(row);
        await db.SaveChangesAsync(ct);
    }

    public async Task RevertToDefaultTemplateAsync(int competitionId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var comp = await db.Competition
            .FirstOrDefaultAsync(c => c.CompetitionID == competitionId, ct)
            ?? throw new KeyNotFoundException("Competition not found.");
        if (!await authzService.CanEditCompetitionAsync(await GetCurrentPrincipalAsync(), comp.HostClubId, competitionId))
            throw new UnauthorizedAccessException("You can't edit this competition.");

        comp.RubberTemplateKey = null;
        var custom = await db.CompetitionRubberTemplates
            .FirstOrDefaultAsync(t => t.CompetitionId == competitionId, ct);
        if (custom is not null) db.CompetitionRubberTemplates.Remove(custom);
        await db.SaveChangesAsync(ct);
    }

    // ── Email ────────────────────────────────────────────────────────────────

    public async Task SendMatchEmailAsync(
        int competitionId, SendMatchEmailRequest request, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var comp = await db.Competition.AsNoTracking()
            .FirstOrDefaultAsync(c => c.CompetitionID == competitionId, ct)
            ?? throw new KeyNotFoundException("Competition not found.");
        if (!await authzService.CanEditCompetitionAsync(await GetCurrentPrincipalAsync(), comp.HostClubId, competitionId))
            throw new UnauthorizedAccessException("You can't email for this competition.");

        var fixture = await db.CompetitionFixtures
            .Include(f => f.Competition)
            .Include(f => f.Player1).Include(f => f.Player2)
            .Include(f => f.Player3).Include(f => f.Player4)
            .FirstOrDefaultAsync(f => f.CompetitionFixtureId == request.FixtureId
                                      && f.CompetitionId == competitionId, ct)
            ?? throw new KeyNotFoundException("Fixture not found.");

        await adminEmailService.SendMatchEmailAsync(fixture, request.Subject, request.Body);
    }

    public async Task SendCompetitionEmailAsync(
        int competitionId, SendCompetitionEmailRequest request, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var comp = await db.Competition.AsNoTracking()
            .FirstOrDefaultAsync(c => c.CompetitionID == competitionId, ct)
            ?? throw new KeyNotFoundException("Competition not found.");
        if (!await authzService.CanEditCompetitionAsync(await GetCurrentPrincipalAsync(), comp.HostClubId, competitionId))
            throw new UnauthorizedAccessException("You can't email for this competition.");

        var participants = await db.CompetitionParticipants
            .Where(cp => cp.CompetitionId == competitionId)
            .Include(cp => cp.Player)
            .Select(cp => cp.Player!)
            .Where(p => p != null && p.Email != null)
            .ToListAsync(ct);

        await adminEmailService.SendCompetitionEmailAsync(
            participants, comp.CompetitionName, request.Subject, request.Body, competitionId);
    }

    // ── Projection helpers ───────────────────────────────────────────────────

    private static CompetitionStageDto ToStageDto(CompetitionStage s) =>
        new(s.CompetitionStageId, s.Name, s.StageOrder, s.StageType);

    private static CompetitionParticipantDto ToParticipantDto(CompetitionParticipant p) =>
        new(p.PlayerId, p.Player?.DisplayName ?? "", p.Status, p.RegisteredAt,
            p.TeamId, p.Team?.Name, p.Player?.MobileNumber, p.Role, p.Player?.Sex,
            p.CompetitionDivisionId, p.Division?.Name);

    private static CompetitionDivisionDto ToDivisionDto(CompetitionDivision d) =>
        new(d.CompetitionDivisionId, d.CompetitionId, d.Rank, d.Name);

    private static CompetitionTeamDto ToTeamDto(CompetitionTeam t) =>
        new(t.CompetitionTeamId, t.CompetitionId, t.Name,
            t.CaptainPlayerId, t.Captain?.DisplayName, t.CompetitionDivisionId);

    private static CompetitionMatchWindowDto ToMatchWindowDto(CompetitionMatchWindow w) =>
        new(w.CompetitionMatchWindowId, w.CompetitionId,
            w.CourtId, w.Court?.Name, w.CompetitionDivisionId, w.Division?.Name,
            w.DayOfWeek, w.StartTime, w.EndTime);

    private static CompetitionFixtureDto ToFixtureDto(CompetitionFixture f) =>
        new(f.CompetitionFixtureId, f.CompetitionId,
            f.CompetitionStageId, f.Stage?.Name,
            f.CourtId, f.Court?.Name,
            f.ScheduledAt, f.Status, f.FixtureLabel, f.RoundNumber,
            f.Player1Id, f.Player1?.DisplayName,
            f.Player2Id, f.Player2?.DisplayName,
            f.Player3Id, f.Player3?.DisplayName,
            f.Player4Id, f.Player4?.DisplayName,
            f.ResultSummary, f.WinnerPlayerId,
            f.HomeTeamId, f.HomeTeam?.Name,
            f.AwayTeamId, f.AwayTeam?.Name,
            f.WinnerTeamId,
            f.CourtPairId,
            f.CourtPair != null
                ? (f.CourtPair.Name ?? $"{f.CourtPair.Court1?.Name} + {f.CourtPair.Court2?.Name}")
                : null,
            null, // Rubbers — admin view doesn't need them at this granularity
            f.CompletedAt,
            f.OriginalResultSummary,
            f.ResultModifiedByAdmin,
            f.Competition?.CompetitionName);

    private static string StageDisplayName(StageType type) => type switch
    {
        StageType.RoundRobin   => "Round Robin",
        StageType.Knockout     => "Knockout",
        StageType.QuarterFinal => "Quarter-Final",
        StageType.SemiFinal    => "Semi-Final",
        StageType.Final        => "Final",
        _                      => type.ToString(),
    };

    private static int StageTypeSortKey(StageType type) => type switch
    {
        StageType.RoundRobin   => 0,
        StageType.QuarterFinal => 1,
        StageType.SemiFinal    => 2,
        StageType.Knockout     => 3,
        StageType.Final        => 4,
        _                      => 99,
    };

    // ── Auth helper ──────────────────────────────────────────────────────────

    private async Task<Competition> LoadAndAuthorizeAsync(MyDbContext db, int competitionId, CancellationToken ct)
    {
        var comp = await db.Competition.AsNoTracking()
            .Where(c => c.CompetitionID == competitionId)
            .Select(c => new Competition
            {
                CompetitionID  = c.CompetitionID,
                HostClubId     = c.HostClubId,
                CompetitionFormat = c.CompetitionFormat,
                EligibleSex    = c.EligibleSex,
                MaxAge         = c.MaxAge,
                MinAge         = c.MinAge,
                MaxParticipants = c.MaxParticipants,
                LeagueScoring  = c.LeagueScoring,
                HasDivisions   = c.HasDivisions,
            })
            .FirstOrDefaultAsync(ct)
            ?? throw new KeyNotFoundException("Competition not found.");
        if (!await authzService.CanEditCompetitionAsync(await GetCurrentPrincipalAsync(), comp.HostClubId, competitionId))
            throw new UnauthorizedAccessException("You can't edit this competition.");
        return comp;
    }

    // ── Participants ─────────────────────────────────────────────────────────

    public async Task<List<PlayerSearchResultDto>> SearchPlayersAsync(
        int competitionId, SearchPlayersForAddRequest request, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var meta = await LoadAndAuthorizeAsync(db, competitionId, ct);

        var alreadyIn = await db.CompetitionParticipants.AsNoTracking()
            .Where(p => p.CompetitionId == competitionId)
            .Select(p => p.PlayerId)
            .ToListAsync(ct);
        var alreadyInSet = alreadyIn.ToHashSet();

        var hostClubId = meta.HostClubId;
        var query = hostClubId.HasValue
            ? db.Players.AsNoTracking().Where(p => db.ClubPlayers.Any(cp => cp.ClubId == hostClubId.Value && cp.PlayerId == p.PlayerId))
            : db.Players.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.Query))
            query = query.Where(p => p.DisplayName.Contains(request.Query));

        var candidates = await query
            .Select(p => new
            {
                p.PlayerId,
                p.DisplayName,
                p.Sex,
                p.DateOfBirth,
                p.IsLight,
                ClubName = hostClubId.HasValue
                    ? db.Clubs.Where(c => c.ClubId == hostClubId.Value).Select(c => c.Name).FirstOrDefault()
                    : null
            })
            .Take((request.MaxResults ?? 20) * 4) // overfetch a little so post-filter still has room
            .ToListAsync(ct);

        var today = DateOnly.FromDateTime(DateTime.Today);
        var results = new List<PlayerSearchResultDto>();
        foreach (var p in candidates)
        {
            if (alreadyInSet.Contains(p.PlayerId)) continue;

            // Gender filter — skip players whose sex doesn't match (allow null/unset through)
            if (meta.EligibleSex.HasValue && p.Sex.HasValue && p.Sex != meta.EligibleSex) continue;

            // Age filter — only apply if player has a date of birth
            if (p.DateOfBirth.HasValue)
            {
                int age = today.Year - p.DateOfBirth.Value.Year;
                if (p.DateOfBirth.Value.AddYears(age) > today) age--;
                if (meta.MaxAge.HasValue && age >= meta.MaxAge.Value) continue;
                if (meta.MinAge.HasValue && age < meta.MinAge.Value) continue;
            }

            results.Add(new PlayerSearchResultDto(
                p.PlayerId, p.DisplayName, p.Sex,
                p.DateOfBirth?.ToDateTime(TimeOnly.MinValue),
                p.ClubName, p.IsLight));
            if (results.Count >= (request.MaxResults ?? 20)) break;
        }
        return results;
    }

    public async Task AddParticipantAsync(int competitionId, AddParticipantRequest request, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var comp = await db.Competition
            .Include(c => c.AllowedPlayers)
            .Include(c => c.Participants)
            .FirstOrDefaultAsync(c => c.CompetitionID == competitionId, ct)
            ?? throw new KeyNotFoundException("Competition not found.");
        if (!await authzService.CanEditCompetitionAsync(await GetCurrentPrincipalAsync(), comp.HostClubId, competitionId))
            throw new UnauthorizedAccessException("You can't edit this competition.");

        if (comp.Participants.Any(p => p.PlayerId == request.PlayerId))
            throw new InvalidOperationException("Player is already a participant.");
        if (comp.MaxParticipants.HasValue && comp.Participants.Count >= comp.MaxParticipants.Value)
            throw new InvalidOperationException($"Maximum of {comp.MaxParticipants.Value} participants reached.");

        var player = await db.Players.FirstOrDefaultAsync(p => p.PlayerId == request.PlayerId, ct)
            ?? throw new KeyNotFoundException("Player not found.");
        // The page logs eligibility violations as admin overrides; mirror that by
        // computing them up-front. The service does NOT block on violations —
        // the caller already accepted them through the page-level dialog.
        _ = EligibilityEvaluator.Evaluate(comp, player);

        db.CompetitionParticipants.Add(new CompetitionParticipant
        {
            CompetitionId = competitionId,
            PlayerId      = request.PlayerId,
            RegisteredAt  = DateTime.UtcNow,
            Status        = ParticipantStatus.Registered,
        });
        await db.SaveChangesAsync(ct);
    }

    public async Task RemoveParticipantAsync(int competitionId, int playerId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        await LoadAndAuthorizeAsync(db, competitionId, ct);
        var row = await db.CompetitionParticipants.FindAsync([competitionId, playerId], ct);
        if (row is null) return;
        db.CompetitionParticipants.Remove(row);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateParticipantStatusAsync(
        int competitionId, int playerId, UpdateParticipantStatusRequest request, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        await LoadAndAuthorizeAsync(db, competitionId, ct);
        var row = await db.CompetitionParticipants.FindAsync([competitionId, playerId], ct)
            ?? throw new KeyNotFoundException("Participant not found.");
        row.Status = request.Status;
        await db.SaveChangesAsync(ct);
    }

    public async Task AssignParticipantTeamAsync(
        int competitionId, int playerId, AssignParticipantTeamRequest request, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        await LoadAndAuthorizeAsync(db, competitionId, ct);
        var row = await db.CompetitionParticipants.FindAsync([competitionId, playerId], ct)
            ?? throw new KeyNotFoundException("Participant not found.");
        row.TeamId = request.TeamId;
        await db.SaveChangesAsync(ct);
    }

    public async Task AssignParticipantRoleAsync(
        int competitionId, int playerId, SetParticipantRoleRequest request, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        await LoadAndAuthorizeAsync(db, competitionId, ct);
        var row = await db.CompetitionParticipants.FindAsync([competitionId, playerId], ct)
            ?? throw new KeyNotFoundException("Participant not found.");
        row.Role = string.IsNullOrWhiteSpace(request.Role) ? null : request.Role;
        await db.SaveChangesAsync(ct);
    }

    public async Task AssignParticipantDivisionAsync(
        int competitionId, int playerId, SetParticipantDivisionRequest request, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        await LoadAndAuthorizeAsync(db, competitionId, ct);
        var row = await db.CompetitionParticipants.FindAsync([competitionId, playerId], ct)
            ?? throw new KeyNotFoundException("Participant not found.");
        row.CompetitionDivisionId = request.CompetitionDivisionId;
        await db.SaveChangesAsync(ct);
    }

    public async Task<int> CreateLightPlayerAsync(
        int competitionId, CreateLightPlayerForCompetitionRequest request, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        await LoadAndAuthorizeAsync(db, competitionId, ct);

        if (string.IsNullOrWhiteSpace(request.DisplayName))
            throw new InvalidOperationException("Display name is required.");
        if (!request.HostClubId.HasValue)
            throw new InvalidOperationException("Light players must be scoped to a host club.");

        var clubId = request.HostClubId.Value;
        var player = new Player
        {
            DisplayName     = request.DisplayName.Trim(),
            FirstName       = string.IsNullOrWhiteSpace(request.FirstName) ? null : request.FirstName!.Trim(),
            LastName        = string.IsNullOrWhiteSpace(request.LastName)  ? null : request.LastName!.Trim(),
            Email           = string.IsNullOrWhiteSpace(request.Email)     ? null : request.Email!.Trim(),
            MobileNumber    = string.IsNullOrWhiteSpace(request.MobileNumber) ? null : request.MobileNumber!.Trim(),
            Sex             = request.Sex,
            DateOfBirth     = request.DateOfBirth.HasValue ? DateOnly.FromDateTime(request.DateOfBirth.Value) : null,
            IsLight         = true,
            CreatedByClubId = clubId,
        };
        db.Players.Add(player);
        await db.SaveChangesAsync(ct);

        db.ClubPlayers.Add(new ClubPlayer { ClubId = clubId, PlayerId = player.PlayerId });
        db.CompetitionParticipants.Add(new CompetitionParticipant
        {
            CompetitionId = competitionId,
            PlayerId      = player.PlayerId,
            RegisteredAt  = DateTime.UtcNow,
            Status        = ParticipantStatus.Registered,
        });
        await db.SaveChangesAsync(ct);
        return player.PlayerId;
    }

    public async Task SaveLightPlayerAsync(
        int competitionId, int playerId, SaveLightPlayerRequest request, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        await LoadAndAuthorizeAsync(db, competitionId, ct);

        var player = await db.Players.FindAsync([playerId], ct)
            ?? throw new KeyNotFoundException("Player not found.");
        if (!player.IsLight)
            throw new InvalidOperationException("Only light players can be edited from this dialog.");

        player.DisplayName  = request.DisplayName.Trim();
        player.FirstName    = string.IsNullOrWhiteSpace(request.FirstName) ? null : request.FirstName!.Trim();
        player.LastName     = string.IsNullOrWhiteSpace(request.LastName)  ? null : request.LastName!.Trim();
        player.Email        = string.IsNullOrWhiteSpace(request.Email)     ? null : request.Email!.Trim();
        player.MobileNumber = string.IsNullOrWhiteSpace(request.MobileNumber) ? null : request.MobileNumber!.Trim();
        player.Sex          = request.Sex;
        player.DateOfBirth  = request.DateOfBirth.HasValue ? DateOnly.FromDateTime(request.DateOfBirth.Value) : null;
        await db.SaveChangesAsync(ct);
    }

    // ── Divisions ────────────────────────────────────────────────────────────

    public async Task<int> SaveDivisionAsync(
        int competitionId, int? divisionId, SaveDivisionRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new InvalidOperationException("Division name is required.");

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var comp = await db.Competition.FirstOrDefaultAsync(c => c.CompetitionID == competitionId, ct)
            ?? throw new KeyNotFoundException("Competition not found.");
        if (!await authzService.CanEditCompetitionAsync(await GetCurrentPrincipalAsync(), comp.HostClubId, competitionId))
            throw new UnauthorizedAccessException("You can't edit this competition.");

        if (divisionId is null)
        {
            if (await db.CompetitionDivisions.AnyAsync(d => d.CompetitionId == competitionId && d.Rank == request.Rank, ct))
                throw new InvalidOperationException($"Division rank {request.Rank} already exists.");
            var div = new CompetitionDivision
            {
                CompetitionId = competitionId,
                Name          = request.Name.Trim(),
                Rank          = request.Rank,
            };
            db.CompetitionDivisions.Add(div);
            if (!comp.HasDivisions) comp.HasDivisions = true;
            await db.SaveChangesAsync(ct);
            return div.CompetitionDivisionId;
        }
        else
        {
            var row = await db.CompetitionDivisions.FindAsync([divisionId.Value], ct)
                ?? throw new KeyNotFoundException("Division not found.");
            if (row.CompetitionId != competitionId)
                throw new InvalidOperationException("Division belongs to a different competition.");
            if (row.Rank != request.Rank
                && await db.CompetitionDivisions.AnyAsync(
                    x => x.CompetitionId == competitionId && x.Rank == request.Rank
                         && x.CompetitionDivisionId != row.CompetitionDivisionId, ct))
                throw new InvalidOperationException($"Division rank {request.Rank} already exists.");
            row.Name = request.Name.Trim();
            row.Rank = request.Rank;
            await db.SaveChangesAsync(ct);
            return row.CompetitionDivisionId;
        }
    }

    public async Task DeleteDivisionAsync(int competitionId, int divisionId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        await LoadAndAuthorizeAsync(db, competitionId, ct);

        var participants = await db.CompetitionParticipants
            .Where(p => p.CompetitionId == competitionId && p.CompetitionDivisionId == divisionId).ToListAsync(ct);
        foreach (var p in participants) p.CompetitionDivisionId = null;

        var teams = await db.CompetitionTeams
            .Where(t => t.CompetitionId == competitionId && t.CompetitionDivisionId == divisionId).ToListAsync(ct);
        foreach (var t in teams) t.CompetitionDivisionId = null;

        var divWindows = await db.CompetitionMatchWindows
            .Where(w => w.CompetitionId == competitionId && w.CompetitionDivisionId == divisionId).ToListAsync(ct);
        if (divWindows.Count > 0) db.CompetitionMatchWindows.RemoveRange(divWindows);

        var row = await db.CompetitionDivisions.FindAsync([divisionId], ct);
        if (row is not null && row.CompetitionId == competitionId)
            db.CompetitionDivisions.Remove(row);
        await db.SaveChangesAsync(ct);
    }

    public async Task RunSeedDivisionsAsync(
        int competitionId, SeedDivisionsFromPreviousRequest request, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var comp = await db.Competition
            .Include(c => c.Divisions)
            .Include(c => c.Participants)
            .FirstOrDefaultAsync(c => c.CompetitionID == competitionId, ct)
            ?? throw new KeyNotFoundException("Competition not found.");
        if (!await authzService.CanEditCompetitionAsync(await GetCurrentPrincipalAsync(), comp.HostClubId, competitionId))
            throw new UnauthorizedAccessException("You can't edit this competition.");

        var previous = await db.Competition
            .Include(c => c.Divisions)
            .Include(c => c.Participants)
            .FirstOrDefaultAsync(c => c.CompetitionID == request.PreviousCompetitionId, ct)
            ?? throw new KeyNotFoundException("Source competition not found.");

        if (comp.Divisions.Any())
            throw new InvalidOperationException("This competition already has divisions — clear them first.");

        var byRank = new Dictionary<byte, CompetitionDivision>();
        foreach (var prev in previous.Divisions.OrderBy(d => d.Rank))
        {
            var nd = new CompetitionDivision
            {
                CompetitionId = competitionId,
                Rank          = prev.Rank,
                Name          = prev.Name,
            };
            db.CompetitionDivisions.Add(nd);
            byRank[prev.Rank] = nd;
        }
        comp.HasDivisions = true;
        comp.SeededFromCompetitionId = previous.CompetitionID;
        await db.SaveChangesAsync(ct);

        var previousRankByPlayer = previous.Participants
            .Where(p => p.CompetitionDivisionId.HasValue)
            .ToDictionary(
                p => p.PlayerId,
                p => previous.Divisions.First(d => d.CompetitionDivisionId == p.CompetitionDivisionId).Rank);

        if (request.ApplyPromotion && (request.PromoteCount > 0 || request.DemoteCount > 0)
            && previous.Divisions.Count > 0)
        {
            byte minRank = previous.Divisions.Min(d => d.Rank);
            byte maxRank = previous.Divisions.Max(d => d.Rank);
            var stats = await PlayerStatsService.ComputeAsync(
                db, [previous.CompetitionID], previous.LeagueScoring);
            foreach (var div in previous.Divisions)
            {
                var ranked = previous.Participants
                    .Where(p => p.CompetitionDivisionId == div.CompetitionDivisionId)
                    .Select(p => new { p.PlayerId, Points = stats.TryGetValue(p.PlayerId, out var s) ? s.LeaguePoints : 0 })
                    .OrderByDescending(x => x.Points)
                    .ToList();
                if (div.Rank > minRank)
                    foreach (var promo in ranked.Take(request.PromoteCount))
                        previousRankByPlayer[promo.PlayerId] = (byte)(div.Rank - 1);
                if (div.Rank < maxRank)
                    foreach (var demo in ranked.AsEnumerable().Reverse().Take(request.DemoteCount))
                        previousRankByPlayer[demo.PlayerId] = (byte)(div.Rank + 1);
            }
        }

        foreach (var cp in comp.Participants)
        {
            if (!previousRankByPlayer.TryGetValue(cp.PlayerId, out var nextRank)) continue;
            if (!byRank.TryGetValue(nextRank, out var div)) continue;
            cp.CompetitionDivisionId = div.CompetitionDivisionId;
        }
        await db.SaveChangesAsync(ct);
    }

    public async Task AssignTeamDivisionAsync(
        int competitionId, int teamId, AssignTeamDivisionRequest request, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        await LoadAndAuthorizeAsync(db, competitionId, ct);
        var team = await db.CompetitionTeams.FindAsync([teamId], ct)
            ?? throw new KeyNotFoundException("Team not found.");
        if (team.CompetitionId != competitionId)
            throw new InvalidOperationException("Team belongs to a different competition.");
        team.CompetitionDivisionId = request.CompetitionDivisionId;
        await db.SaveChangesAsync(ct);
    }

    // ── Teams ────────────────────────────────────────────────────────────────

    public async Task<int> SaveTeamAsync(
        int competitionId, int? teamId, SaveTeamRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new InvalidOperationException("Team name is required.");

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        await LoadAndAuthorizeAsync(db, competitionId, ct);

        if (teamId is null)
        {
            var team = new CompetitionTeam { CompetitionId = competitionId, Name = request.Name.Trim() };
            db.CompetitionTeams.Add(team);
            await db.SaveChangesAsync(ct);
            return team.CompetitionTeamId;
        }
        else
        {
            var row = await db.CompetitionTeams.FindAsync([teamId.Value], ct)
                ?? throw new KeyNotFoundException("Team not found.");
            if (row.CompetitionId != competitionId)
                throw new InvalidOperationException("Team belongs to a different competition.");
            row.Name = request.Name.Trim();
            await db.SaveChangesAsync(ct);
            return row.CompetitionTeamId;
        }
    }

    public async Task DeleteTeamAsync(int competitionId, int teamId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        await LoadAndAuthorizeAsync(db, competitionId, ct);

        var members = await db.CompetitionParticipants
            .Where(cp => cp.CompetitionId == competitionId && cp.TeamId == teamId).ToListAsync(ct);
        foreach (var m in members) m.TeamId = null;

        var row = await db.CompetitionTeams.FindAsync([teamId], ct);
        if (row is not null && row.CompetitionId == competitionId)
            db.CompetitionTeams.Remove(row);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAllTeamsAsync(int competitionId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        await LoadAndAuthorizeAsync(db, competitionId, ct);

        var participants = await db.CompetitionParticipants
            .Where(cp => cp.CompetitionId == competitionId && cp.TeamId != null).ToListAsync(ct);
        foreach (var cp in participants) cp.TeamId = null;

        var fixtures = await db.CompetitionFixtures
            .Where(f => f.CompetitionId == competitionId
                     && (f.HomeTeamId != null || f.AwayTeamId != null || f.WinnerTeamId != null))
            .ToListAsync(ct);
        var fixtureIds = fixtures.Select(f => f.CompetitionFixtureId).ToList();
        foreach (var f in fixtures)
        {
            f.HomeTeamId = null;
            f.AwayTeamId = null;
            f.WinnerTeamId = null;
        }
        if (fixtureIds.Count > 0)
        {
            var rubbers = await db.Rubbers
                .Where(r => fixtureIds.Contains(r.CompetitionFixtureId) && r.WinnerTeamId != null)
                .ToListAsync(ct);
            foreach (var r in rubbers) r.WinnerTeamId = null;
        }

        var teams = await db.CompetitionTeams.Where(t => t.CompetitionId == competitionId).ToListAsync(ct);
        db.CompetitionTeams.RemoveRange(teams);
        await db.SaveChangesAsync(ct);
    }

    public async Task AssignCaptainAsync(
        int competitionId, AssignCaptainRequest request, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        await LoadAndAuthorizeAsync(db, competitionId, ct);
        var team = await db.CompetitionTeams.FindAsync([request.CompetitionTeamId], ct)
            ?? throw new KeyNotFoundException("Team not found.");
        if (team.CompetitionId != competitionId)
            throw new InvalidOperationException("Team belongs to a different competition.");
        team.CaptainPlayerId = request.PlayerId;
        await db.SaveChangesAsync(ct);
    }

    public async Task<int> AutoAssignCaptainsAsync(int competitionId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        await LoadAndAuthorizeAsync(db, competitionId, ct);

        var teams = await db.CompetitionTeams
            .Where(t => t.CompetitionId == competitionId && t.CaptainPlayerId == null)
            .ToListAsync(ct);
        if (teams.Count == 0) return 0;

        var participants = await db.CompetitionParticipants.AsNoTracking()
            .Where(p => p.CompetitionId == competitionId
                        && p.Status == ParticipantStatus.FullPlayer
                        && p.TeamId != null)
            .Select(p => new { p.PlayerId, p.TeamId })
            .ToListAsync(ct);

        var byTeam = participants.GroupBy(p => p.TeamId!.Value)
                                 .ToDictionary(g => g.Key, g => g.Select(x => x.PlayerId).ToList());
        var rng = new Random();
        int assigned = 0;
        foreach (var team in teams)
        {
            if (!byTeam.TryGetValue(team.CompetitionTeamId, out var ids) || ids.Count == 0) continue;
            team.CaptainPlayerId = ids[rng.Next(ids.Count)];
            assigned++;
        }
        if (assigned > 0) await db.SaveChangesAsync(ct);
        return assigned;
    }

    public async Task<GenerateTeamsResultDto> GenerateTeamsPreviewAsync(
        int competitionId, GenerateTeamsRequest request, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var comp = await db.Competition
            .Include(c => c.Divisions)
            .FirstOrDefaultAsync(c => c.CompetitionID == competitionId, ct)
            ?? throw new KeyNotFoundException("Competition not found.");
        if (!await authzService.CanEditCompetitionAsync(await GetCurrentPrincipalAsync(), comp.HostClubId, competitionId))
            throw new UnauthorizedAccessException("You can't edit this competition.");

        var participants = await db.CompetitionParticipants.AsNoTracking()
            .Include(p => p.Player)
            .Where(p => p.CompetitionId == competitionId)
            .ToListAsync(ct);

        var warnings = new List<string>();
        var preview = new List<GeneratedTeamPreviewDto>();

        var active = participants.Where(p => p.Status == ParticipantStatus.FullPlayer).ToList();
        var skippedRegistered = participants.Count(p => p.Status == ParticipantStatus.Registered);
        var skippedDisqualified = participants.Count(p => p.Status == ParticipantStatus.Disqualified);
        if (skippedRegistered > 0)
            warnings.Add($"{skippedRegistered} participant(s) with status 'Registered' will be skipped — only Full Players are included.");
        if (skippedDisqualified > 0)
            warnings.Add($"{skippedDisqualified} disqualified participant(s) will be skipped.");
        if (active.Count == 0)
        {
            warnings.Add("No Full Players to generate from.");
            return new GenerateTeamsResultDto(preview, warnings);
        }

        var format = comp.CompetitionFormat;
        var isDoubles = format is CompetitionFormat.Doubles or CompetitionFormat.MixedDoubles;
        var isTeamMatch = format == CompetitionFormat.TeamMatch;
        var itemLabel = isDoubles ? "Pair" : "Team";
        var teamSize = request.TeamSize ?? comp.TeamSize ?? (isDoubles ? 2 : 4);

        // For TeamMatch, derive role assigner from the persisted preset key (or
        // the format default). The page also looks at the in-memory rubber
        // template state; here we use the source-of-truth columns on Competition.
        RubberTemplateRegistry.RoleAssigner? assigner = null;
        if (isTeamMatch)
        {
            var presetKey = !string.IsNullOrEmpty(comp.RubberTemplateKey)
                ? comp.RubberTemplateKey
                : RubberTemplateRegistry.GetFormatDefaultKey(format);
            assigner = RubberTemplateRegistry.GetRoleAssigner(presetKey);
        }

        var useMttSplit = isTeamMatch
            && (comp.RubberTemplateKey == RubberTemplateRegistry.MttKey
                || string.IsNullOrEmpty(comp.RubberTemplateKey));

        List<(int? DivisionId, string? DivisionName, List<CompetitionParticipant> Members)> buckets;
        if (comp.HasDivisions && comp.Divisions.Count > 0)
        {
            // Filter by requested division if specified.
            var divs = request.CompetitionDivisionId.HasValue
                ? comp.Divisions.Where(d => d.CompetitionDivisionId == request.CompetitionDivisionId.Value).ToList()
                : comp.Divisions.OrderBy(d => d.Rank).ToList();
            var assigned = active.Where(p => p.CompetitionDivisionId.HasValue).ToList();
            var unassigned = active.Count - assigned.Count;
            if (unassigned > 0 && !request.CompetitionDivisionId.HasValue)
                warnings.Add($"{unassigned} participant(s) have no division set and will be skipped.");

            buckets = divs
                .Select(d => ((int?)d.CompetitionDivisionId, (string?)d.Name,
                              assigned.Where(p => p.CompetitionDivisionId == d.CompetitionDivisionId).ToList()))
                .Where(t => t.Item3.Count > 0)
                .ToList();
            if (buckets.Count == 0)
            {
                warnings.Add("No participants are assigned to any division — nothing to generate.");
                return new GenerateTeamsResultDto(preview, warnings);
            }
        }
        else
        {
            buckets = [(null, null, active)];
        }

        var rng = new Random();
        foreach (var (divId, divName, bucketMembers) in buckets)
        {
            GenerateBucket(bucketMembers, divId, divName, format,
                useMttSplit || (request.BalanceByGender && format == CompetitionFormat.MixedDoubles),
                teamSize, itemLabel, assigner, rng, preview, warnings);
        }
        return new GenerateTeamsResultDto(preview, warnings);
    }

    private static void GenerateBucket(
        List<CompetitionParticipant> bucketMembers,
        int? divisionId, string? divisionName,
        CompetitionFormat? format, bool splitByGender,
        int teamSize, string itemLabel,
        RubberTemplateRegistry.RoleAssigner? assigner, Random rng,
        List<GeneratedTeamPreviewDto> preview, List<string> warnings)
    {
        string TeamName(int idx) => $"{itemLabel} {(char)('A' + idx)}";
        string BucketPrefix() => divisionName is null ? "" : $"[{divisionName}] ";

        if (splitByGender)
        {
            var males   = bucketMembers.Where(p => p.Player?.Sex == PlayerSex.Male).OrderBy(_ => rng.Next()).ToList();
            var females = bucketMembers.Where(p => p.Player?.Sex == PlayerSex.Female).OrderBy(_ => rng.Next()).ToList();
            var noSex   = bucketMembers.Where(p => p.Player?.Sex is null or PlayerSex.Other).ToList();

            if (noSex.Count > 0)
                warnings.Add($"{BucketPrefix()}{noSex.Count} player(s) have no gender set and will be skipped.");

            var perGender = teamSize / 2;
            if (teamSize % 2 != 0)
                warnings.Add($"{BucketPrefix()}Team size {teamSize} is odd — using {perGender} of each gender.");

            var teamCount = Math.Min(males.Count / perGender, females.Count / perGender);
            if (teamCount == 0)
            {
                warnings.Add($"{BucketPrefix()}Not enough players: {males.Count} male(s), {females.Count} female(s) for teams of {teamSize}.");
                return;
            }
            var leftoverM = males.Count - (teamCount * perGender);
            var leftoverF = females.Count - (teamCount * perGender);
            if (leftoverM > 0 || leftoverF > 0)
                warnings.Add($"{BucketPrefix()}{leftoverM + leftoverF} player(s) will be unassigned ({leftoverM} male, {leftoverF} female).");

            for (int i = 0; i < teamCount; i++)
            {
                var members = new List<CompetitionParticipant>();
                members.AddRange(males.Skip(i * perGender).Take(perGender));
                members.AddRange(females.Skip(i * perGender).Take(perGender));
                preview.Add(new GeneratedTeamPreviewDto(
                    TeamName(i),
                    members.Select(m => m.PlayerId).ToList(),
                    AssignRoles(assigner, members),
                    divisionId, divisionName));
            }
        }
        else
        {
            var shuffled = bucketMembers.OrderBy(_ => rng.Next()).ToList();
            var teamCount = shuffled.Count / teamSize;
            var leftover = shuffled.Count % teamSize;
            if (teamCount == 0)
            {
                warnings.Add($"{BucketPrefix()}Not enough players ({shuffled.Count}) for teams of {teamSize}.");
                return;
            }
            if (leftover > 0)
                warnings.Add($"{BucketPrefix()}{leftover} player(s) will be unassigned.");
            for (int i = 0; i < teamCount; i++)
            {
                var members = shuffled.Skip(i * teamSize).Take(teamSize).ToList();
                preview.Add(new GeneratedTeamPreviewDto(
                    TeamName(i),
                    members.Select(m => m.PlayerId).ToList(),
                    AssignRoles(assigner, members),
                    divisionId, divisionName));
            }
        }
    }

    private static IReadOnlyDictionary<int, string> AssignRoles(
        RubberTemplateRegistry.RoleAssigner? assigner, List<CompetitionParticipant> members)
    {
        if (assigner is null) return new Dictionary<int, string>();
        var candidates = members
            .Select(m => new RubberTemplateRegistry.AssignmentCandidate(
                m.PlayerId, m.Player?.DisplayName ?? "", m.Player?.Sex))
            .ToList();
        return assigner(candidates).ToDictionary(kv => kv.Key, kv => kv.Value);
    }

    public async Task ConfirmGenerateTeamsAsync(
        int competitionId, ConfirmGenerateTeamsRequest request, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var comp = await db.Competition.FirstOrDefaultAsync(c => c.CompetitionID == competitionId, ct)
            ?? throw new KeyNotFoundException("Competition not found.");
        if (!await authzService.CanEditCompetitionAsync(await GetCurrentPrincipalAsync(), comp.HostClubId, competitionId))
            throw new UnauthorizedAccessException("You can't edit this competition.");

        var existingTeams = await db.CompetitionTeams.Where(t => t.CompetitionId == competitionId).ToListAsync(ct);
        if (existingTeams.Count > 0)
        {
            var existingMembers = await db.CompetitionParticipants
                .Where(cp => cp.CompetitionId == competitionId && cp.TeamId != null)
                .ToListAsync(ct);
            foreach (var m in existingMembers) m.TeamId = null;
            db.CompetitionTeams.RemoveRange(existingTeams);
            await db.SaveChangesAsync(ct);
        }

        int? appliedTeamSize = null;
        foreach (var preview in request.Teams)
        {
            var team = new CompetitionTeam
            {
                CompetitionId         = competitionId,
                Name                  = preview.Name,
                CompetitionDivisionId = preview.DivisionId,
            };
            db.CompetitionTeams.Add(team);
            await db.SaveChangesAsync(ct);

            foreach (var memberId in preview.MemberPlayerIds)
            {
                var cp = await db.CompetitionParticipants.FindAsync([competitionId, memberId], ct);
                if (cp is null) continue;
                cp.TeamId = team.CompetitionTeamId;
                if (preview.Roles.TryGetValue(memberId, out var role)) cp.Role = role;
            }
            appliedTeamSize ??= preview.MemberPlayerIds.Count;
            await db.SaveChangesAsync(ct);
        }

        if (appliedTeamSize.HasValue && comp.TeamSize != appliedTeamSize.Value)
        {
            comp.TeamSize = appliedTeamSize.Value;
            await db.SaveChangesAsync(ct);
        }
    }

    public async Task<ValidateTeamResultDto> ValidateTeamAsync(
        int competitionId, int teamId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var comp = await db.Competition.AsNoTracking()
            .FirstOrDefaultAsync(c => c.CompetitionID == competitionId, ct)
            ?? throw new KeyNotFoundException("Competition not found.");
        if (!await authzService.CanEditCompetitionAsync(await GetCurrentPrincipalAsync(), comp.HostClubId, competitionId))
            throw new UnauthorizedAccessException("You can't edit this competition.");

        var team = await db.CompetitionTeams.AsNoTracking()
            .FirstOrDefaultAsync(t => t.CompetitionTeamId == teamId && t.CompetitionId == competitionId, ct)
            ?? throw new KeyNotFoundException("Team not found.");

        var members = await db.CompetitionParticipants.AsNoTracking()
            .Include(p => p.Player)
            .Where(p => p.CompetitionId == competitionId
                        && p.TeamId == teamId
                        && p.Status == ParticipantStatus.FullPlayer)
            .ToListAsync(ct);

        var resolved = await rubberTemplateProvider.GetAsync(db, competitionId);
        var requiredRoles = RubberTemplateRegistry.GetRoleSet(resolved).ToList();

        var errors = new List<string>();
        var warnings = new List<string>();
        if (requiredRoles.Count > 0)
        {
            foreach (var role in requiredRoles)
            {
                int count = members.Count(m => m.Role == role);
                if (count == 0) errors.Add($"Missing role {role}");
                else if (count > 1) errors.Add($"Duplicate role {role}");
            }
            foreach (var m in members.Where(m => string.IsNullOrEmpty(m.Role)))
                errors.Add($"{m.Player?.DisplayName}: no role");
        }
        return new ValidateTeamResultDto(errors, warnings);
    }

    // ── Fixtures ─────────────────────────────────────────────────────────────

    public async Task<int> SaveFixtureAsync(
        int competitionId, int? fixtureId, SaveFixtureRequest request, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var comp = await db.Competition.AsNoTracking()
            .FirstOrDefaultAsync(c => c.CompetitionID == competitionId, ct)
            ?? throw new KeyNotFoundException("Competition not found.");
        if (!await authzService.CanEditCompetitionAsync(await GetCurrentPrincipalAsync(), comp.HostClubId, competitionId))
            throw new UnauthorizedAccessException("You can't edit this competition.");

        var side1 = new[] { request.Player1Id, request.Player3Id }.Where(id => id.HasValue).Select(id => id!.Value).ToHashSet();
        var side2 = new[] { request.Player2Id, request.Player4Id }.Where(id => id.HasValue).Select(id => id!.Value).ToHashSet();
        if (side1.Overlaps(side2))
            throw new InvalidOperationException("A player cannot appear on both sides of a fixture.");

        bool isTeamMatch = comp.CompetitionFormat == CompetitionFormat.TeamMatch;

        if (fixtureId is null)
        {
            var f = new CompetitionFixture { CompetitionId = competitionId };
            ApplyFixtureRequest(f, request, isTeamMatch);
            db.CompetitionFixtures.Add(f);
            await db.SaveChangesAsync(ct);
            return f.CompetitionFixtureId;
        }
        else
        {
            var f = await db.CompetitionFixtures.FindAsync([fixtureId.Value], ct)
                ?? throw new KeyNotFoundException("Fixture not found.");
            if (f.CompetitionId != competitionId)
                throw new InvalidOperationException("Fixture belongs to a different competition.");
            ApplyFixtureRequest(f, request, isTeamMatch);
            await db.SaveChangesAsync(ct);
            return f.CompetitionFixtureId;
        }
    }

    private static void ApplyFixtureRequest(CompetitionFixture f, SaveFixtureRequest req, bool isTeamMatch)
    {
        f.CompetitionStageId = req.CompetitionStageId;
        f.FixtureLabel       = req.FixtureLabel;
        f.ScheduledAt        = req.ScheduledAt;
        f.Player1Id          = req.Player1Id;
        f.Player2Id          = req.Player2Id;
        f.Player3Id          = req.Player3Id;
        f.Player4Id          = req.Player4Id;
        f.HomeTeamId         = req.HomeTeamId;
        f.AwayTeamId         = req.AwayTeamId;
        if (req.RoundNumber.HasValue) f.RoundNumber = req.RoundNumber.Value;
        f.Status             = req.Status;
        if (isTeamMatch)
        {
            f.CourtPairId = req.CourtPairId;
            f.CourtId     = null;
        }
        else
        {
            f.CourtId     = req.CourtId;
            f.CourtPairId = null;
        }
    }

    public async Task DeleteFixtureAsync(int competitionId, int fixtureId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        await LoadAndAuthorizeAsync(db, competitionId, ct);

        var fx = await db.CompetitionFixtures
            .Include(f => f.Stage)
            .FirstOrDefaultAsync(f => f.CompetitionFixtureId == fixtureId, ct);
        if (fx is null || fx.CompetitionId != competitionId) return;

        bool hasResult = fx.Status == FixtureStatus.Completed
                      || fx.Status == FixtureStatus.AwaitingVerification;

        if (!hasResult)
        {
            db.CompetitionFixtures.Remove(fx);
            await db.SaveChangesAsync(ct);
            return;
        }

        // Has a result: keep the row, but reset to Scheduled and cascade across
        // any downstream fixtures that inherited a winner from this one.
        var oldWinnerPlayerId = fx.WinnerPlayerId;
        var oldWinnerTeamId   = fx.WinnerTeamId;
        bool isTeamFixture    = fx.HomeTeamId.HasValue;
        var currentStageOrder = fx.Stage?.StageOrder;
        var currentRound      = fx.RoundNumber;

        bool IsDownstream(CompetitionFixture f)
        {
            if (currentStageOrder.HasValue && f.Stage != null && f.Stage.StageOrder > currentStageOrder.Value) return true;
            if (f.CompetitionStageId == fx.CompetitionStageId
                && currentRound.HasValue && f.RoundNumber.HasValue
                && f.RoundNumber.Value > currentRound.Value) return true;
            return false;
        }

        if (oldWinnerPlayerId.HasValue || oldWinnerTeamId.HasValue)
        {
            var downstream = await db.CompetitionFixtures
                .Include(f => f.Stage)
                .Where(f => f.CompetitionId == fx.CompetitionId
                    && f.CompetitionFixtureId != fx.CompetitionFixtureId
                    && (f.Status == FixtureStatus.InProgress || f.Status == FixtureStatus.Completed
                        || f.Status == FixtureStatus.AwaitingVerification))
                .ToListAsync(ct);
            bool blocked = isTeamFixture
                ? downstream.Any(f => IsDownstream(f) && (f.HomeTeamId == oldWinnerTeamId || f.AwayTeamId == oldWinnerTeamId))
                : downstream.Any(f => IsDownstream(f) && (f.Player1Id == oldWinnerPlayerId || f.Player2Id == oldWinnerPlayerId));
            if (blocked)
                throw new InvalidOperationException(
                    "Cannot delete: the winner has a downstream match that is in progress or completed. Delete that result first.");
        }

        if (oldWinnerPlayerId.HasValue)
        {
            var nextFixtures = await db.CompetitionFixtures
                .Include(f => f.Stage)
                .Where(f => f.CompetitionId == fx.CompetitionId
                    && f.CompetitionFixtureId != fx.CompetitionFixtureId
                    && f.Status == FixtureStatus.Scheduled
                    && (f.Player1Id == oldWinnerPlayerId || f.Player2Id == oldWinnerPlayerId))
                .ToListAsync(ct);
            foreach (var nf in nextFixtures)
            {
                if (!IsDownstream(nf)) continue;
                if (nf.Player1Id == oldWinnerPlayerId) nf.Player1Id = null;
                if (nf.Player2Id == oldWinnerPlayerId) nf.Player2Id = null;
            }
        }

        if (oldWinnerTeamId.HasValue)
        {
            var nextFixtures = await db.CompetitionFixtures
                .Include(f => f.Stage)
                .Include(f => f.Rubbers)
                .Where(f => f.CompetitionId == fx.CompetitionId
                    && f.CompetitionFixtureId != fx.CompetitionFixtureId
                    && f.Status == FixtureStatus.Scheduled
                    && (f.HomeTeamId == oldWinnerTeamId || f.AwayTeamId == oldWinnerTeamId))
                .ToListAsync(ct);
            foreach (var nf in nextFixtures)
            {
                if (!IsDownstream(nf)) continue;
                if (nf.HomeTeamId == oldWinnerTeamId) nf.HomeTeamId = null;
                if (nf.AwayTeamId == oldWinnerTeamId) nf.AwayTeamId = null;
                if (nf.Rubbers.Any()) db.Rubbers.RemoveRange(nf.Rubbers);
            }
        }

        if (fx.SavedMatchId.HasValue)
        {
            var savedMatch = await db.SavedMatch.FindAsync([fx.SavedMatchId.Value], ct);
            if (savedMatch is not null) db.SavedMatch.Remove(savedMatch);
            fx.SavedMatchId = null;
        }

        if (isTeamFixture)
        {
            var rubbers = await db.Rubbers
                .Where(r => r.CompetitionFixtureId == fx.CompetitionFixtureId)
                .ToListAsync(ct);
            var rubberSavedMatchIds = rubbers
                .Where(r => r.SavedMatchId.HasValue)
                .Select(r => r.SavedMatchId!.Value)
                .ToList();
            if (rubberSavedMatchIds.Count > 0)
            {
                var perRubberSavedMatches = await db.SavedMatch
                    .Where(m => rubberSavedMatchIds.Contains(m.SavedMatchId))
                    .ToListAsync(ct);
                db.SavedMatch.RemoveRange(perRubberSavedMatches);
            }
            foreach (var r in rubbers)
            {
                r.IsComplete       = false;
                r.WinnerTeamId     = null;
                r.HomeSetsWon      = null;
                r.AwaySetsWon      = null;
                r.HomeGames        = null;
                r.AwayGames        = null;
                r.HomeGamesTotal   = null;
                r.AwayGamesTotal   = null;
                r.SavedMatchId     = null;
            }
        }

        fx.Status                 = FixtureStatus.Scheduled;
        fx.CompletedAt            = null;
        fx.ResultSummary          = null;
        fx.WinnerPlayerId         = null;
        fx.WinnerTeamId           = null;
        fx.VerificationToken      = null;
        fx.OriginalResultSummary  = null;
        fx.OriginalWinnerPlayerId = null;
        fx.ResultModifiedByAdmin  = false;
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAllFixturesAsync(int competitionId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        await LoadAndAuthorizeAsync(db, competitionId, ct);

        var fixtures = await db.CompetitionFixtures
            .Where(f => f.CompetitionId == competitionId).ToListAsync(ct);
        var fixtureIds = fixtures.Select(f => f.CompetitionFixtureId).ToList();

        var rubbers = await db.Rubbers.Where(r => fixtureIds.Contains(r.CompetitionFixtureId)).ToListAsync(ct);
        var rubberSavedMatchIds = rubbers
            .Where(r => r.SavedMatchId.HasValue)
            .Select(r => r.SavedMatchId!.Value)
            .ToList();
        if (rubbers.Count > 0) db.Rubbers.RemoveRange(rubbers);

        var directSavedMatchIds = fixtures.Where(f => f.SavedMatchId.HasValue).Select(f => f.SavedMatchId!.Value);
        var savedMatchIds = directSavedMatchIds.Concat(rubberSavedMatchIds).Distinct().ToList();
        if (savedMatchIds.Count > 0)
        {
            var savedMatches = await db.SavedMatch.Where(m => savedMatchIds.Contains(m.SavedMatchId)).ToListAsync(ct);
            if (savedMatches.Count > 0) db.SavedMatch.RemoveRange(savedMatches);
        }

        db.CompetitionFixtures.RemoveRange(fixtures);
        await db.SaveChangesAsync(ct);
    }

    public async Task<CompetitionFixtureDto?> LoadFixtureForDialogAsync(
        int competitionId, int fixtureId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var comp = await db.Competition.AsNoTracking()
            .FirstOrDefaultAsync(c => c.CompetitionID == competitionId, ct);
        if (comp is null) return null;
        if (!await authzService.CanEditCompetitionAsync(await GetCurrentPrincipalAsync(), comp.HostClubId, competitionId))
            throw new UnauthorizedAccessException("You can't view this competition's fixtures.");

        var f = await db.CompetitionFixtures.AsNoTracking()
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
            .Include(f => f.Competition)
            .FirstOrDefaultAsync(f => f.CompetitionFixtureId == fixtureId && f.CompetitionId == competitionId, ct);
        return f is null ? null : ToFixtureDto(f);
    }

    public async Task<ConfirmFixtureAssignmentResultDto> ConfirmFixtureAssignmentAsync(
        int competitionId, ConfirmFixtureAssignmentRequest request, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var comp = await db.Competition.AsNoTracking()
            .Include(c => c.AllowedPlayers)
            .FirstOrDefaultAsync(c => c.CompetitionID == competitionId, ct)
            ?? throw new KeyNotFoundException("Competition not found.");
        if (!await authzService.CanEditCompetitionAsync(await GetCurrentPrincipalAsync(), comp.HostClubId, competitionId))
            throw new UnauthorizedAccessException("You can't edit this competition.");

        var playerIds = new[] { request.Player1Id, request.Player2Id, request.Player3Id, request.Player4Id }
            .Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToList();
        if (playerIds.Count == 0)
            return new ConfirmFixtureAssignmentResultDto(true, []);

        var participantIds = await db.CompetitionParticipants.AsNoTracking()
            .Where(p => p.CompetitionId == competitionId && p.Status == ParticipantStatus.FullPlayer)
            .Select(p => p.PlayerId).ToListAsync(ct);
        var participantSet = participantIds.ToHashSet();
        var missing = playerIds.Where(pid => !participantSet.Contains(pid)).ToList();
        var violations = new List<string>();
        if (missing.Count > 0)
        {
            var names = await db.Players.AsNoTracking()
                .Where(p => missing.Contains(p.PlayerId))
                .Select(p => p.DisplayName)
                .ToListAsync(ct);
            violations.Add($"Player(s) {string.Join(", ", names)} are not registered Full Player participants of this competition.");
            return new ConfirmFixtureAssignmentResultDto(false, violations);
        }

        var players = await db.Players.AsNoTracking().Where(p => playerIds.Contains(p.PlayerId)).ToListAsync(ct);
        foreach (var p in players)
            foreach (var v in EligibilityEvaluator.Evaluate(comp, p))
                violations.Add($"{p.DisplayName}: {v.Message}");

        if (comp.CompetitionFormat == CompetitionFormat.MixedDoubles)
        {
            void CheckPair(int? aId, int? bId, string label)
            {
                if (!aId.HasValue || !bId.HasValue) return;
                var a = players.FirstOrDefault(p => p.PlayerId == aId.Value);
                var b = players.FirstOrDefault(p => p.PlayerId == bId.Value);
                if (a is null || b is null) return;
                if (a.Sex.HasValue && b.Sex.HasValue && a.Sex == b.Sex)
                    violations.Add($"{label} is two {(a.Sex == PlayerSex.Male ? "males" : "females")}; mixed doubles requires one male + one female.");
            }
            CheckPair(request.Player1Id, request.Player3Id, "Home pair");
            CheckPair(request.Player2Id, request.Player4Id, "Away pair");
        }

        return new ConfirmFixtureAssignmentResultDto(violations.Count == 0, violations);
    }

    public async Task<ScheduleFixturesResultDto> ScheduleFixturesAsync(
        int competitionId, ScheduleFixturesAdminRequest request, CancellationToken ct = default)
    {
        await using (var db = await dbFactory.CreateDbContextAsync(ct))
            await LoadAndAuthorizeAsync(db, competitionId, ct);

        var mode = request.DeleteExistingUnscheduled
            ? ScheduleDeleteMode.UnscheduledOnly
            : ScheduleDeleteMode.None;
        var result = await scheduler.ScheduleAsync(competitionId, new ScheduleFixturesRequest(mode));
        return new ScheduleFixturesResultDto(
            result.FixturesCreated, result.Unscheduled, result.UnconfirmedParticipants);
    }

    public async Task<SimulateRoundRobinResultDto> SimulateRoundRobinAsync(int competitionId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        await LoadAndAuthorizeAsync(db, competitionId, ct);
        if (!authzService.IsSuperAdmin(await GetCurrentPrincipalAsync()))
            throw new UnauthorizedAccessException("Only super admins can simulate fixtures.");

        var outcome = await fixtureSimulator.SimulateRoundRobinAsync(db, competitionId);
        return new SimulateRoundRobinResultDto(outcome.FixturesSimulated);
    }

    public async Task<SeedKnockoutFromStandingsResultDto> SeedKnockoutFromStandingsAsync(
        int competitionId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var comp = await db.Competition
            .Include(c => c.Stages)
            .Include(c => c.Participants)
            .FirstOrDefaultAsync(c => c.CompetitionID == competitionId, ct)
            ?? throw new KeyNotFoundException("Competition not found.");
        if (!await authzService.CanEditCompetitionAsync(await GetCurrentPrincipalAsync(), comp.HostClubId, competitionId))
            throw new UnauthorizedAccessException("You can't edit this competition.");

        var warnings = new List<string>();
        var knockoutStageIds = comp.Stages
            .Where(s => s.StageType is StageType.Knockout or StageType.QuarterFinal)
            .Select(s => s.CompetitionStageId)
            .ToHashSet();

        var firstRoundFixtures = await db.CompetitionFixtures
            .Where(f => f.CompetitionId == competitionId
                     && f.Status != FixtureStatus.Completed
                     && f.CompetitionStageId.HasValue
                     && knockoutStageIds.Contains(f.CompetitionStageId.Value)
                     && f.RoundNumber == 1)
            .OrderBy(f => f.FixtureLabel)
            .ToListAsync(ct);

        if (firstRoundFixtures.Count == 0)
        {
            warnings.Add("No unseeded knockout fixtures found.");
            return new SeedKnockoutFromStandingsResultDto(0, warnings);
        }

        var rrStageIds = comp.Stages
            .Where(s => s.StageType == StageType.RoundRobin)
            .Select(s => s.CompetitionStageId)
            .ToHashSet();

        var activePlayers = comp.Participants
            .Where(p => p.Status == ParticipantStatus.FullPlayer)
            .Select(p => p.PlayerId)
            .ToList();

        List<int> seededPlayers;
        var rrFixtures = await db.CompetitionFixtures
            .Where(f => f.CompetitionId == competitionId
                     && f.CompetitionStageId != null
                     && rrStageIds.Contains(f.CompetitionStageId!.Value)
                     && f.Status == FixtureStatus.Completed
                     && f.WinnerPlayerId != null)
            .ToListAsync(ct);

        if (rrFixtures.Count == 0)
        {
            seededPlayers = activePlayers.Take(firstRoundFixtures.Count * 2).ToList();
        }
        else
        {
            var pts = activePlayers.ToDictionary(pid => pid, _ => (Points: 0, Won: 0));
            foreach (var f in rrFixtures)
            {
                var pids = new[] { f.Player1Id, f.Player2Id, f.Player3Id, f.Player4Id }
                    .Where(pid => pid.HasValue && pts.ContainsKey(pid.Value))
                    .Select(pid => pid!.Value).Distinct();
                foreach (var pid in pids)
                {
                    bool win = pid == f.WinnerPlayerId;
                    int points = comp.LeagueScoring switch
                    {
                        LeagueScoringMode.SetsWon  => ParseSetsWon(f.ResultSummary, pid == f.Player1Id || pid == f.Player3Id),
                        LeagueScoringMode.GamesWon => ParseGamesWon(f.ResultSummary, pid == f.Player1Id || pid == f.Player3Id),
                        _ => win ? 3 : (!f.WinnerPlayerId.HasValue ? 1 : 0),
                    };
                    var cur = pts[pid];
                    pts[pid] = (cur.Points + points, cur.Won + (win ? 1 : 0));
                }
            }
            seededPlayers = pts
                .OrderByDescending(kv => kv.Value.Points)
                .ThenByDescending(kv => kv.Value.Won)
                .Take(firstRoundFixtures.Count * 2)
                .Select(kv => kv.Key)
                .ToList();
        }

        CompetitionProgressionService.AssignWinners(firstRoundFixtures, seededPlayers);
        await db.SaveChangesAsync(ct);
        return new SeedKnockoutFromStandingsResultDto(firstRoundFixtures.Count, warnings);
    }

    private static int ParseSetsWon(string? resultSummary, bool isSide1)
    {
        if (string.IsNullOrWhiteSpace(resultSummary)) return 0;
        int count = 0;
        foreach (var set in resultSummary.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = set.Split('-');
            if (parts.Length != 2 || !int.TryParse(parts[0], out var g1) || !int.TryParse(parts[1], out var g2)) continue;
            if (isSide1 && g1 > g2) count++;
            else if (!isSide1 && g2 > g1) count++;
        }
        return count;
    }

    private static int ParseGamesWon(string? resultSummary, bool isSide1)
    {
        if (string.IsNullOrWhiteSpace(resultSummary)) return 0;
        int total = 0;
        foreach (var set in resultSummary.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = set.Split('-');
            if (parts.Length != 2 || !int.TryParse(parts[0], out var g1) || !int.TryParse(parts[1], out var g2)) continue;
            total += isSide1 ? g1 : g2;
        }
        return total;
    }

    // ── Match windows ────────────────────────────────────────────────────────

    public async Task<int> AddMatchWindowAsync(
        int competitionId, SaveMatchWindowRequest request, CancellationToken ct = default)
    {
        if (request.EndTime <= request.StartTime)
            throw new InvalidOperationException("End time must be after start time.");

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        await LoadAndAuthorizeAsync(db, competitionId, ct);

        var w = new CompetitionMatchWindow
        {
            CompetitionId         = competitionId,
            CourtId               = request.CourtId,
            CompetitionDivisionId = request.CompetitionDivisionId,
            DayOfWeek             = request.DayOfWeek,
            StartTime             = request.StartTime,
            EndTime               = request.EndTime,
        };
        db.CompetitionMatchWindows.Add(w);
        await db.SaveChangesAsync(ct);
        return w.CompetitionMatchWindowId;
    }

    public async Task DeleteMatchWindowAsync(int competitionId, int matchWindowId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        await LoadAndAuthorizeAsync(db, competitionId, ct);

        var w = await db.CompetitionMatchWindows.FindAsync([matchWindowId], ct);
        if (w is null || w.CompetitionId != competitionId) return;
        db.CompetitionMatchWindows.Remove(w);
        await db.SaveChangesAsync(ct);
    }

    public async Task<int> AddDivisionMatchWindowAsync(
        int competitionId, int divisionId, SaveMatchWindowRequest request, CancellationToken ct = default)
    {
        if (request.EndTime <= request.StartTime)
            throw new InvalidOperationException("End time must be after start time.");

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        await LoadAndAuthorizeAsync(db, competitionId, ct);

        var w = new CompetitionMatchWindow
        {
            CompetitionId         = competitionId,
            CompetitionDivisionId = divisionId,
            CourtId               = request.CourtId,
            DayOfWeek             = request.DayOfWeek,
            StartTime             = request.StartTime,
            EndTime               = request.EndTime,
        };
        db.CompetitionMatchWindows.Add(w);
        await db.SaveChangesAsync(ct);
        return w.CompetitionMatchWindowId;
    }

    public async Task<int> ImportMatchWindowsFromTemplateAsync(
        int competitionId, ImportMatchWindowsFromTemplateRequest request, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        await LoadAndAuthorizeAsync(db, competitionId, ct);

        var template = await db.ClubSchedulingTemplates.AsNoTracking()
            .Include(t => t.Windows)
            .FirstOrDefaultAsync(t => t.ClubSchedulingTemplateId == request.ClubSchedulingTemplateId, ct)
            ?? throw new KeyNotFoundException("Template not found.");

        var existing = await db.CompetitionMatchWindows.AsNoTracking()
            .Where(w => w.CompetitionId == competitionId)
            .Select(w => new { w.DayOfWeek, w.StartTime, w.EndTime })
            .ToListAsync(ct);
        var existingKeys = existing.Select(e => (e.DayOfWeek, e.StartTime, e.EndTime)).ToHashSet();

        var toAdd = template.Windows
            .Where(tw => !existingKeys.Contains((tw.DayOfWeek, tw.StartTime, tw.EndTime)))
            .Select(tw => new CompetitionMatchWindow
            {
                CompetitionId = competitionId,
                DayOfWeek     = tw.DayOfWeek,
                StartTime     = tw.StartTime,
                EndTime       = tw.EndTime,
            })
            .ToList();
        if (toAdd.Count == 0) return 0;

        db.CompetitionMatchWindows.AddRange(toAdd);
        await db.SaveChangesAsync(ct);
        return toAdd.Count;
    }
}
