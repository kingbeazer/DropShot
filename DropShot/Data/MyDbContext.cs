using DropShot.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace DropShot.Data
{
    public class MyDbContext(DbContextOptions<MyDbContext> options) : IdentityDbContext<ApplicationUser>(options)
    {
        public DbSet<Competition> Competition { get; set; }
        public DbSet<Score> Score { get; set; }
        public DbSet<SavedMatch> SavedMatch { get; set; }
        public DbSet<AppSetting> AppSettings { get; set; }
        public DbSet<Player> Players { get; set; }

        public DbSet<RulesSet> RulesSets { get; set; }
        public DbSet<RulesSetItem> RulesSetItems { get; set; }
        public DbSet<Club> Clubs { get; set; }
        public DbSet<Court> Courts { get; set; }
        public DbSet<ClubMember> ClubMembers { get; set; }
        public DbSet<ClubAdministrator> ClubAdministrators { get; set; }
        public DbSet<CompetitionParticipant> CompetitionParticipants { get; set; }
        public DbSet<CompetitionStage> CompetitionStages { get; set; }
        public DbSet<CompetitionFixture> CompetitionFixtures { get; set; }
        public DbSet<CompetitionTeam> CompetitionTeams { get; set; }
        public DbSet<ClubLadder> ClubLadders { get; set; }
        public DbSet<LadderEntry> LadderEntries { get; set; }
        public DbSet<PlayerFriend> PlayerFriends { get; set; }
        public DbSet<CompetitionMatchWindow> CompetitionMatchWindows { get; set; }
        public DbSet<ClubSchedulingTemplate> ClubSchedulingTemplates { get; set; }
        public DbSet<ClubSchedulingTemplateWindow> ClubSchedulingTemplateWindows { get; set; }
        public DbSet<CompetitionAdmin> CompetitionAdmins { get; set; }
        public DbSet<CompetitionTemplate> CompetitionTemplates { get; set; }
        public DbSet<CompetitionTemplateWindow> CompetitionTemplateWindows { get; set; }
        public DbSet<ClubEmailTemplate> ClubEmailTemplates { get; set; }
        public DbSet<ScoreboardDisplaySetting> ScoreboardDisplaySettings { get; set; }
        public DbSet<UserPlayer> UserPlayers { get; set; }
        public DbSet<RoleSwitchLog> RoleSwitchLogs { get; set; }
        public DbSet<Event> Events { get; set; }
        public DbSet<CourtPair> CourtPairs { get; set; }
        public DbSet<TeamMatchSet> TeamMatchSets { get; set; }
        public DbSet<PlayerInvitation> PlayerInvitations { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // ── PlayerInvitation ────────────────────────────────────────────────
            builder.Entity<PlayerInvitation>(entity =>
            {
                entity.Property(i => i.CreatedByUserId).HasMaxLength(450).IsRequired();
                entity.Property(i => i.AcceptedByUserId).HasMaxLength(450);
                entity.Property(i => i.SentToEmail).HasMaxLength(256);
                entity.HasIndex(i => i.Token).IsUnique();
                entity.HasIndex(i => i.CreatedByUserId);

                entity.HasOne(i => i.LightPlayer)
                      .WithMany()
                      .HasForeignKey(i => i.LightPlayerId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(i => i.CreatedByUser)
                      .WithMany()
                      .HasForeignKey(i => i.CreatedByUserId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // ── Player ──────────────────────────────────────────────────────────
            builder.Entity<Player>(entity =>
            {
                entity.Property(p => p.DisplayName).HasMaxLength(100).IsRequired();
                entity.Property(p => p.Email).HasMaxLength(256);
                entity.Property(p => p.FirstName).HasMaxLength(100);
                entity.Property(p => p.LastName).HasMaxLength(100);
                entity.Property(p => p.ProfileImagePath).HasMaxLength(500);
                entity.Property(p => p.ContactPreferences).HasMaxLength(50);
                entity.Property(p => p.MobileNumber).HasMaxLength(20);
                entity.Property(p => p.Sex).HasConversion<byte?>();

                entity.HasOne(p => p.User)
                      .WithMany()
                      .HasForeignKey(p => p.UserId)
                      .OnDelete(DeleteBehavior.SetNull);

                entity.Property(p => p.IsLight).HasDefaultValue(false);
                entity.Property(p => p.CreatedByUserId).HasMaxLength(450);
                entity.HasIndex(p => p.CreatedByUserId);

                entity.HasOne(p => p.CreatedByUser)
                      .WithMany()
                      .HasForeignKey(p => p.CreatedByUserId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // ── Competition ──────────────────────────────────────────────────────
            builder.Entity<Competition>(entity =>
            {
                entity.Property(c => c.CompetitionName).HasMaxLength(200).IsRequired();
                entity.Property(c => c.EligibleSex).HasConversion<byte?>();

                entity.HasOne(c => c.Rules)
                      .WithMany(r => r.Competitions)
                      .HasForeignKey(c => c.RulesSetId)
                      .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(c => c.HostClub)
                      .WithMany(cl => cl.Competitions)
                      .HasForeignKey(c => c.HostClubId)
                      .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(c => c.Event)
                      .WithMany(e => e.Competitions)
                      .HasForeignKey(c => c.EventId)
                      .OnDelete(DeleteBehavior.SetNull);
            });

            // ── Event ───────────────────────────────────────────────────────────────
            builder.Entity<Event>(entity =>
            {
                entity.Property(e => e.Name).HasMaxLength(200).IsRequired();
                entity.Property(e => e.Description).HasMaxLength(1000);

                entity.HasOne(e => e.HostClub)
                      .WithMany(c => c.Events)
                      .HasForeignKey(e => e.HostClubId)
                      .OnDelete(DeleteBehavior.SetNull);
            });

            // ── RulesSet / RulesSetItem ──────────────────────────────────────────
            builder.Entity<RulesSet>(entity =>
            {
                entity.Property(r => r.Name).HasMaxLength(200).IsRequired();

                entity.HasOne(r => r.Club)
                      .WithMany(c => c.RulesSets)
                      .HasForeignKey(r => r.ClubId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<RulesSetItem>(entity =>
            {
                entity.Property(i => i.RuleText).HasMaxLength(500).IsRequired();

                entity.HasOne(i => i.RulesSet)
                      .WithMany(r => r.Items)
                      .HasForeignKey(i => i.RulesSetId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // ── Club / Court ─────────────────────────────────────────────────────
            builder.Entity<Club>(entity =>
            {
                entity.Property(c => c.Name).HasMaxLength(200).IsRequired();
                entity.Property(c => c.AddressLine1).HasMaxLength(200);
                entity.Property(c => c.AddressLine2).HasMaxLength(200);
                entity.Property(c => c.Town).HasMaxLength(100);
                entity.Property(c => c.Postcode).HasMaxLength(20);
                entity.Property(c => c.Phone).HasMaxLength(50);
                entity.Property(c => c.Email).HasMaxLength(256);
                entity.Property(c => c.Website).HasMaxLength(500);
            });

            builder.Entity<Court>(entity =>
            {
                entity.Property(c => c.Name).HasMaxLength(100).IsRequired();
                entity.Property(c => c.Surface).HasConversion<byte>();

                entity.HasOne(c => c.Club)
                      .WithMany(cl => cl.Courts)
                      .HasForeignKey(c => c.ClubId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // ── ClubMember ───────────────────────────────────────────────────────
            builder.Entity<ClubMember>(entity =>
            {
                entity.HasKey(cm => new { cm.ClubId, cm.PlayerId });

                entity.HasOne(cm => cm.Club)
                      .WithMany(c => c.Members)
                      .HasForeignKey(cm => cm.ClubId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(cm => cm.Player)
                      .WithMany(p => p.ClubMemberships)
                      .HasForeignKey(cm => cm.PlayerId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // ── ClubAdministrator ────────────────────────────────────────────────
            builder.Entity<ClubAdministrator>(entity =>
            {
                entity.HasKey(ca => new { ca.UserId, ca.ClubId });

                entity.HasOne(ca => ca.User)
                      .WithMany()
                      .HasForeignKey(ca => ca.UserId)
                      .OnDelete(DeleteBehavior.Restrict);   // block user deletion while club admin assignments exist

                entity.HasOne(ca => ca.Club)
                      .WithMany(c => c.Administrators)
                      .HasForeignKey(ca => ca.ClubId)
                      .OnDelete(DeleteBehavior.Cascade);    // deleting a club removes its admin assignments
            });

            // ── CompetitionTeam ──────────────────────────────────────────────────
            builder.Entity<CompetitionTeam>(entity =>
            {
                entity.Property(t => t.Name).HasMaxLength(100).IsRequired();

                entity.HasOne(t => t.Competition)
                      .WithMany(c => c.Teams)
                      .HasForeignKey(t => t.CompetitionId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(t => t.Captain)
                      .WithMany()
                      .HasForeignKey(t => t.CaptainPlayerId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // ── CompetitionParticipant ───────────────────────────────────────────
            builder.Entity<CompetitionParticipant>(entity =>
            {
                entity.HasKey(cp => new { cp.CompetitionId, cp.PlayerId });
                entity.Property(cp => cp.Status).HasConversion<byte>();
                entity.Property(cp => cp.Grade).HasConversion<byte?>();

                entity.HasOne(cp => cp.Competition)
                      .WithMany(c => c.Participants)
                      .HasForeignKey(cp => cp.CompetitionId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(cp => cp.Player)
                      .WithMany(p => p.CompetitionParticipants)
                      .HasForeignKey(cp => cp.PlayerId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(cp => cp.Team)
                      .WithMany(t => t.Participants)
                      .HasForeignKey(cp => cp.TeamId)
                      .OnDelete(DeleteBehavior.NoAction);
            });

            // ── CompetitionStage ─────────────────────────────────────────────────
            builder.Entity<CompetitionStage>(entity =>
            {
                entity.Property(s => s.Name).HasMaxLength(100).IsRequired();
                entity.Property(s => s.StageType).HasConversion<byte>();

                entity.HasOne(s => s.Competition)
                      .WithMany(c => c.Stages)
                      .HasForeignKey(s => s.CompetitionId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // ── CompetitionFixture ───────────────────────────────────────────────
            builder.Entity<CompetitionFixture>(entity =>
            {
                entity.Property(f => f.Status).HasConversion<byte>();
                entity.Property(f => f.ResultSummary).HasMaxLength(200);
                entity.Property(f => f.OriginalResultSummary).HasMaxLength(200);
                entity.Property(f => f.FixtureLabel).HasMaxLength(50);

                entity.HasOne(f => f.Competition)
                      .WithMany(c => c.Fixtures)
                      .HasForeignKey(f => f.CompetitionId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(f => f.Stage)
                      .WithMany(s => s.Fixtures)
                      .HasForeignKey(f => f.CompetitionStageId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(f => f.Court)
                      .WithMany(c => c.Fixtures)
                      .HasForeignKey(f => f.CourtId)
                      .OnDelete(DeleteBehavior.SetNull);

                // Multiple FKs to Player — all must be Restrict to avoid multi-cascade-path error
                entity.HasOne(f => f.Player1).WithMany().HasForeignKey(f => f.Player1Id).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(f => f.Player2).WithMany().HasForeignKey(f => f.Player2Id).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(f => f.Player3).WithMany().HasForeignKey(f => f.Player3Id).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(f => f.Player4).WithMany().HasForeignKey(f => f.Player4Id).OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(f => f.SavedMatch)
                      .WithMany()
                      .HasForeignKey(f => f.SavedMatchId)
                      .OnDelete(DeleteBehavior.SetNull);

                // Mixed Team Tennis FKs — all NoAction to avoid multi-cascade through Competition
                entity.HasOne(f => f.HomeTeam).WithMany().HasForeignKey(f => f.HomeTeamId).OnDelete(DeleteBehavior.NoAction);
                entity.HasOne(f => f.AwayTeam).WithMany().HasForeignKey(f => f.AwayTeamId).OnDelete(DeleteBehavior.NoAction);
                entity.HasOne(f => f.WinnerTeam).WithMany().HasForeignKey(f => f.WinnerTeamId).OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(f => f.CourtPair)
                      .WithMany()
                      .HasForeignKey(f => f.CourtPairId)
                      .OnDelete(DeleteBehavior.NoAction);
            });

            // ── CompetitionMatchWindow ───────────────────────────────────────────
            builder.Entity<CompetitionMatchWindow>(entity =>
            {
                entity.Property(w => w.DayOfWeek).HasConversion<int>();

                entity.HasOne(w => w.Competition)
                      .WithMany(c => c.MatchWindows)
                      .HasForeignKey(w => w.CompetitionId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(w => w.Court)
                      .WithMany()
                      .HasForeignKey(w => w.CourtId)
                      .OnDelete(DeleteBehavior.SetNull);
            });

            // ── ClubSchedulingTemplate / ClubSchedulingTemplateWindow ────────────
            builder.Entity<ClubSchedulingTemplate>(entity =>
            {
                entity.Property(t => t.Name).HasMaxLength(200).IsRequired();

                entity.HasOne(t => t.Club)
                      .WithMany(c => c.SchedulingTemplates)
                      .HasForeignKey(t => t.ClubId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<ClubSchedulingTemplateWindow>(entity =>
            {
                entity.Property(w => w.DayOfWeek).HasConversion<int>();

                entity.HasOne(w => w.Template)
                      .WithMany(t => t.Windows)
                      .HasForeignKey(w => w.ClubSchedulingTemplateId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // ── CompetitionAdmin ─────────────────────────────────────────────────
            builder.Entity<CompetitionAdmin>(entity =>
            {
                entity.HasKey(ca => new { ca.CompetitionId, ca.UserId });

                entity.HasOne(ca => ca.Competition)
                      .WithMany(c => c.Admins)
                      .HasForeignKey(ca => ca.CompetitionId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(ca => ca.User)
                      .WithMany()
                      .HasForeignKey(ca => ca.UserId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // ── CompetitionTemplate / CompetitionTemplateWindow ──────────────────
            builder.Entity<CompetitionTemplate>(entity =>
            {
                entity.Property(t => t.Name).HasMaxLength(200).IsRequired();
                entity.Property(t => t.Format).HasConversion<byte?>();
                entity.Property(t => t.EligibleSex).HasConversion<byte?>();

                entity.HasOne(t => t.Club)
                      .WithMany(c => c.CompetitionTemplates)
                      .HasForeignKey(t => t.ClubId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<CompetitionTemplateWindow>(entity =>
            {
                entity.Property(w => w.DayOfWeek).HasConversion<int>();

                entity.HasOne(w => w.Template)
                      .WithMany(t => t.Windows)
                      .HasForeignKey(w => w.CompetitionTemplateId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // ── ClubEmailTemplate ────────────────────────────────────────────────
            builder.Entity<ClubEmailTemplate>(entity =>
            {
                entity.Property(t => t.Name).HasMaxLength(200).IsRequired();
                entity.Property(t => t.Subject).HasMaxLength(500).IsRequired();
                entity.Property(t => t.Body).HasColumnType("nvarchar(max)").IsRequired();

                entity.HasOne(t => t.Club)
                      .WithMany(c => c.EmailTemplates)
                      .HasForeignKey(t => t.ClubId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // ── SavedMatch ───────────────────────────────────────────────────────
            builder.Entity<SavedMatch>(entity =>
            {
                entity.HasOne<Court>()
                      .WithMany()
                      .HasForeignKey(m => m.CourtId)
                      .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne<Player>().WithMany().HasForeignKey(m => m.Player1Id).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne<Player>().WithMany().HasForeignKey(m => m.Player2Id).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne<Player>().WithMany().HasForeignKey(m => m.Player3Id).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne<Player>().WithMany().HasForeignKey(m => m.Player4Id).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne<Player>().WithMany().HasForeignKey(m => m.WinnerPlayerId).OnDelete(DeleteBehavior.Restrict);
            });

            // ── ScoreboardDisplaySetting ─────────────────────────────────────────
            builder.Entity<ScoreboardDisplaySetting>(entity =>
            {
                entity.Property(s => s.Layout).HasMaxLength(20).HasDefaultValue("default");
                entity.Property(s => s.LiveStreamUrl).HasMaxLength(500);

                entity.HasIndex(s => s.CourtId).IsUnique();

                entity.HasOne(s => s.Court)
                      .WithOne()
                      .HasForeignKey<ScoreboardDisplaySetting>(s => s.CourtId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // ── PlayerFriend (self-referential) ──────────────────────────────────
            builder.Entity<PlayerFriend>(entity =>
            {
                entity.HasKey(pf => new { pf.PlayerId, pf.FriendPlayerId });
                entity.Property(pf => pf.Status).HasConversion<byte>();

                // Both sides Restrict — SQL Server cannot cascade on self-referential tables
                entity.HasOne(pf => pf.Player)
                      .WithMany(p => p.Friends)
                      .HasForeignKey(pf => pf.PlayerId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(pf => pf.Friend)
                      .WithMany(p => p.FriendOf)
                      .HasForeignKey(pf => pf.FriendPlayerId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // ── ClubLadder / LadderEntry ─────────────────────────────────────────
            builder.Entity<ClubLadder>(entity =>
            {
                entity.Property(l => l.Name).HasMaxLength(200).IsRequired();

                entity.HasOne(l => l.Club)
                      .WithMany(c => c.Ladders)
                      .HasForeignKey(l => l.ClubId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(l => l.Rules)
                      .WithMany(r => r.Ladders)
                      .HasForeignKey(l => l.RulesSetId)
                      .OnDelete(DeleteBehavior.SetNull);
            });

            builder.Entity<LadderEntry>(entity =>
            {
                entity.HasIndex(le => new { le.ClubLadderId, le.PlayerId }).IsUnique();

                entity.HasOne(le => le.Ladder)
                      .WithMany(l => l.Entries)
                      .HasForeignKey(le => le.ClubLadderId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(le => le.Player)
                      .WithMany()
                      .HasForeignKey(le => le.PlayerId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // ── UserPlayer (My Players bookmark) ────────────────────────────────
            builder.Entity<UserPlayer>(entity =>
            {
                entity.HasKey(up => new { up.UserId, up.PlayerId });

                entity.HasOne(up => up.User)
                      .WithMany()
                      .HasForeignKey(up => up.UserId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(up => up.Player)
                      .WithMany()
                      .HasForeignKey(up => up.PlayerId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // ── CourtPair ───────────────────────────────────────────────────────
            builder.Entity<CourtPair>(entity =>
            {
                entity.Property(cp => cp.Name).HasMaxLength(100).IsRequired();

                entity.HasOne(cp => cp.Competition)
                      .WithMany(c => c.CourtPairs)
                      .HasForeignKey(cp => cp.CompetitionId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(cp => cp.Court1)
                      .WithMany()
                      .HasForeignKey(cp => cp.Court1Id)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(cp => cp.Court2)
                      .WithMany()
                      .HasForeignKey(cp => cp.Court2Id)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // ── TeamMatchSet ───────────────────────────────────────────────────
            builder.Entity<TeamMatchSet>(entity =>
            {
                entity.Property(s => s.Phase).HasConversion<byte>();
                entity.Property(s => s.SetType).HasConversion<byte>();

                entity.HasOne(s => s.Fixture)
                      .WithMany(f => f.TeamMatchSets)
                      .HasForeignKey(s => s.CompetitionFixtureId)
                      .OnDelete(DeleteBehavior.Cascade);

                // Multiple player FKs — all Restrict
                entity.HasOne(s => s.HomePlayer1).WithMany().HasForeignKey(s => s.HomePlayer1Id).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(s => s.HomePlayer2).WithMany().HasForeignKey(s => s.HomePlayer2Id).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(s => s.AwayPlayer1).WithMany().HasForeignKey(s => s.AwayPlayer1Id).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(s => s.AwayPlayer2).WithMany().HasForeignKey(s => s.AwayPlayer2Id).OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(s => s.WinnerTeam)
                      .WithMany()
                      .HasForeignKey(s => s.WinnerTeamId)
                      .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(s => s.SavedMatch)
                      .WithMany()
                      .HasForeignKey(s => s.SavedMatchId)
                      .OnDelete(DeleteBehavior.SetNull);
            });

            // ── RoleSwitchLog ───────────────────────────────────────────────────
            builder.Entity<RoleSwitchLog>(entity =>
            {
                entity.Property(r => r.UserId).HasMaxLength(450).IsRequired();
                entity.Property(r => r.FromRole).HasMaxLength(50).IsRequired();
                entity.Property(r => r.ToRole).HasMaxLength(50).IsRequired();
                entity.Property(r => r.IpAddress).HasMaxLength(45);
                entity.HasIndex(r => r.UserId);
                entity.HasIndex(r => r.Timestamp);
            });
        }
    }

    public interface ISettingsService
    {
        Task<Dictionary<string, string>> GetSettingsAsync();
    }

    public class SettingsService : ISettingsService
    {
        private readonly MyDbContext _context;

        public SettingsService(MyDbContext context)
        {
            _context = context;
        }

        public async Task<Dictionary<string, string>> GetSettingsAsync()
        {
            return await _context.AppSettings
                .ToDictionaryAsync(s => s.Setting, s => s.Value);
        }
    }
}
