using System.Security.Claims;
using DropShot.Data;
using DropShot.Models;
using DropShot.Shared;
using DropShot.Shared.Dtos;
using DropShot.UI.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace DropShot.Services;

public sealed class WebCompetitionAdminService(
    IDbContextFactory<MyDbContext> dbFactory,
    ClubAuthorizationService authzService,
    IHttpContextAccessor httpContextAccessor,
    UserManager<ApplicationUser> userManager,
    ICompetitionRubberTemplateProvider rubberTemplateProvider,
    AdminEmailService adminEmailService) : ICompetitionAdminService
{
    private ClaimsPrincipal CurrentUser =>
        httpContextAccessor.HttpContext?.User ?? new ClaimsPrincipal();

    // ── Read ─────────────────────────────────────────────────────────────────

    public async Task<CompetitionEditDto?> GetCompetitionForEditAsync(int? competitionId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var clubs = await db.Clubs.AsNoTracking().OrderBy(c => c.Name)
            .Select(c => new ClubDto(c.ClubId, c.Name, c.AddressLine1, c.AddressLine2, c.Town, c.Postcode,
                c.Phone, c.Email, c.Website, c.Courts.Count))
            .ToListAsync(ct);
        var rulesSets = await db.RulesSets.AsNoTracking().OrderBy(r => r.Name)
            .Select(r => new RulesSetDto(r.RulesSetId, r.Name, r.Description, r.Items.Count))
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
            var isSuperAdminCreate = authzService.IsSuperAdmin(CurrentUser);
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

        if (!await authzService.CanEditCompetitionAsync(CurrentUser, comp.HostClubId, id))
            return null;

        var canEdit = true;
        var isSuperAdmin = authzService.IsSuperAdmin(CurrentUser);

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
            return await authzService.CanEditCompetitionAsync(CurrentUser, hostClubId, competitionId.Value);
        }

        var appUser = await userManager.GetUserAsync(CurrentUser);
        var canCreateUserComp = authzService.CanCreateUserCompetition(CurrentUser, appUser);
        var isAdmin = await authzService.IsAdminAsync(CurrentUser);
        var adminClubIds = await authzService.GetAdminClubIdsAsync(CurrentUser);
        return isAdmin || adminClubIds.Count > 0 || canCreateUserComp;
    }

    public Task<bool> IsSuperAdminAsync(CancellationToken ct = default)
        => Task.FromResult(authzService.IsSuperAdmin(CurrentUser));

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

            if (!await authzService.CanEditCompetitionAsync(CurrentUser, comp.HostClubId, comp.CompetitionID))
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
                comp.CreatorUserId = userManager.GetUserId(CurrentUser);
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

        if (!await authzService.CanEditCompetitionAsync(CurrentUser, source.HostClubId, sourceCompetitionId))
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

        if (!await authzService.CanEditCompetitionAsync(CurrentUser, comp.HostClubId, competitionId))
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

        if (!await authzService.CanEditCompetitionAsync(CurrentUser, comp.HostClubId, competitionId))
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
        if (!await authzService.CanEditCompetitionAsync(CurrentUser, comp.HostClubId, competitionId))
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
        if (!await authzService.CanEditCompetitionAsync(CurrentUser, comp.HostClubId, competitionId))
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
        if (!await authzService.CanEditCompetitionAsync(CurrentUser, comp.HostClubId, competitionId))
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
        if (!await authzService.CanEditCompetitionAsync(CurrentUser, comp.HostClubId, competitionId))
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
        if (!await authzService.CanEditCompetitionAsync(CurrentUser, comp.HostClubId, competitionId))
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
        if (!await authzService.CanEditCompetitionAsync(CurrentUser, comp.HostClubId, competitionId))
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
        if (!await authzService.CanEditCompetitionAsync(CurrentUser, comp.HostClubId, competitionId))
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
        if (!await authzService.CanEditCompetitionAsync(CurrentUser, comp.HostClubId, competitionId))
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
        if (!await authzService.CanEditCompetitionAsync(CurrentUser, comp.HostClubId, competitionId))
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
        if (!await authzService.CanEditCompetitionAsync(CurrentUser, comp.HostClubId, competitionId))
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
}
