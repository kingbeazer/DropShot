using DropShot.Models;
using DropShot.Services;
using DropShot.Shared;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DropShot.Tests.Helpers;

public class PlayerRatingServiceTests
{
    private static PlayerRatingService BuildService(TestDbContextFactory factory) => new(factory);

    [Fact]
    public async Task NoPriorSnapshot_ReturnsDefault()
    {
        var factory = new TestDbContextFactory();
        using (var db = factory.CreateDbContext())
        {
            db.Players.Add(new Player { PlayerId = 1, DisplayName = "A" });
            db.Competition.Add(new Competition
            {
                CompetitionID = 100, CompetitionName = "Parent"
            });
            db.Competition.Add(new Competition
            {
                CompetitionID = 200, CompetitionName = "Child",
                SeededFromCompetitionId = 100
            });
            await db.SaveChangesAsync();
        }

        var svc = BuildService(factory);
        var rating = await svc.GetCurrentRatingAsync(playerId: 1, competitionId: 200);

        Assert.Equal(PlayerRatingService.DefaultRating, rating.Value);
        Assert.True(rating.IsProvisional);
    }

    [Fact]
    public async Task ParentHasSeasonEnd_ReturnsIt()
    {
        var factory = new TestDbContextFactory();
        using (var db = factory.CreateDbContext())
        {
            db.Players.Add(new Player { PlayerId = 1, DisplayName = "A" });
            db.Competition.Add(new Competition { CompetitionID = 100, CompetitionName = "Parent" });
            db.Competition.Add(new Competition
            {
                CompetitionID = 200, CompetitionName = "Child",
                SeededFromCompetitionId = 100
            });
            db.PlayerRatingSnapshots.Add(new PlayerRatingSnapshot
            {
                PlayerId = 1,
                CompetitionId = 100,
                Kind = PlayerRatingSnapshotKind.SeasonEnd,
                Rating = 1620,
                RubbersPlayed = 15,
                IsProvisional = false
            });
            await db.SaveChangesAsync();
        }

        var svc = BuildService(factory);
        var rating = await svc.GetCurrentRatingAsync(playerId: 1, competitionId: 200);

        Assert.Equal(1620, rating.Value);
        Assert.False(rating.IsProvisional);
    }

    [Fact]
    public async Task ChainSkipsParent_ReturnsParentSeasonStart_NotGrandparent()
    {
        // grandparent (Z) has SeasonEnd=1700; parent (Y) was a season the player
        // didn't play in, but they carried in via Y.SeasonStart=1700; child (X)
        // looks at Y only and must return Y.SeasonStart, never walking back to Z.
        var factory = new TestDbContextFactory();
        using (var db = factory.CreateDbContext())
        {
            db.Players.Add(new Player { PlayerId = 1, DisplayName = "A" });
            db.Competition.Add(new Competition { CompetitionID = 100, CompetitionName = "Z" });
            db.Competition.Add(new Competition
            {
                CompetitionID = 200, CompetitionName = "Y",
                SeededFromCompetitionId = 100
            });
            db.Competition.Add(new Competition
            {
                CompetitionID = 300, CompetitionName = "X",
                SeededFromCompetitionId = 200
            });
            db.PlayerRatingSnapshots.Add(new PlayerRatingSnapshot
            {
                PlayerId = 1, CompetitionId = 100,
                Kind = PlayerRatingSnapshotKind.SeasonEnd,
                Rating = 1700, RubbersPlayed = 20, IsProvisional = false
            });
            db.PlayerRatingSnapshots.Add(new PlayerRatingSnapshot
            {
                PlayerId = 1, CompetitionId = 200,
                Kind = PlayerRatingSnapshotKind.SeasonStart,
                Rating = 1700, RubbersPlayed = 0, IsProvisional = false
            });
            // No Y.SeasonEnd (player didn't play in Y).
            await db.SaveChangesAsync();
        }

        var svc = BuildService(factory);
        var rating = await svc.GetCurrentRatingAsync(playerId: 1, competitionId: 300);

        Assert.Equal(1700, rating.Value);
        Assert.False(rating.IsProvisional);

        // Sanity check: the service should NOT walk past parent. Confirm by also
        // removing Y.SeasonStart and asserting we fall back to default (not Z).
    }

    [Fact]
    public async Task ChainSkipsParent_NoParentSnapshots_ReturnsDefault_NotGrandparent()
    {
        var factory = new TestDbContextFactory();
        using (var db = factory.CreateDbContext())
        {
            db.Players.Add(new Player { PlayerId = 1, DisplayName = "A" });
            db.Competition.Add(new Competition { CompetitionID = 100, CompetitionName = "Z" });
            db.Competition.Add(new Competition
            {
                CompetitionID = 200, CompetitionName = "Y",
                SeededFromCompetitionId = 100
            });
            db.Competition.Add(new Competition
            {
                CompetitionID = 300, CompetitionName = "X",
                SeededFromCompetitionId = 200
            });
            // Z has a SeasonEnd but Y has no snapshots at all — service must
            // return default, NOT walk back to Z.
            db.PlayerRatingSnapshots.Add(new PlayerRatingSnapshot
            {
                PlayerId = 1, CompetitionId = 100,
                Kind = PlayerRatingSnapshotKind.SeasonEnd,
                Rating = 1700, RubbersPlayed = 20, IsProvisional = false
            });
            await db.SaveChangesAsync();
        }

        var svc = BuildService(factory);
        var rating = await svc.GetCurrentRatingAsync(playerId: 1, competitionId: 300);

        Assert.Equal(PlayerRatingService.DefaultRating, rating.Value);
        Assert.True(rating.IsProvisional);
    }

    [Fact]
    public async Task NoParentCompetition_ReturnsDefault()
    {
        var factory = new TestDbContextFactory();
        using (var db = factory.CreateDbContext())
        {
            db.Competition.Add(new Competition { CompetitionID = 100, CompetitionName = "Standalone" });
            await db.SaveChangesAsync();
        }

        var svc = BuildService(factory);
        var rating = await svc.GetCurrentRatingAsync(playerId: 1, competitionId: 100);

        Assert.Equal(PlayerRatingService.DefaultRating, rating.Value);
        Assert.True(rating.IsProvisional);
    }

    [Fact]
    public async Task SetInitialRating_InsertsSeasonStart_OnCurrentCompetition()
    {
        var factory = new TestDbContextFactory();
        using (var db = factory.CreateDbContext())
        {
            db.Players.Add(new Player { PlayerId = 1, DisplayName = "A" });
            db.Competition.Add(new Competition { CompetitionID = 100, CompetitionName = "Standalone" });
            db.CompetitionParticipants.Add(new CompetitionParticipant { CompetitionId = 100, PlayerId = 1 });
            await db.SaveChangesAsync();
        }

        var svc = BuildService(factory);
        await svc.SetInitialRatingAsync(competitionId: 100, playerId: 1, rating: 1450, acceptedByUserId: "admin");

        using var dbCheck = factory.CreateDbContext();
        var snap = await dbCheck.PlayerRatingSnapshots.SingleAsync();
        Assert.Equal(100, snap.CompetitionId);
        Assert.Equal(PlayerRatingSnapshotKind.SeasonStart, snap.Kind);
        Assert.Equal(1450, snap.Rating);
        Assert.False(snap.IsProvisional);
        Assert.Equal("admin", snap.AcceptedByUserId);
    }

    [Fact]
    public async Task SetInitialRating_IsIdempotent_UpdatesExistingRow()
    {
        var factory = new TestDbContextFactory();
        using (var db = factory.CreateDbContext())
        {
            db.Players.Add(new Player { PlayerId = 1, DisplayName = "A" });
            db.Competition.Add(new Competition { CompetitionID = 100, CompetitionName = "Standalone" });
            db.CompetitionParticipants.Add(new CompetitionParticipant { CompetitionId = 100, PlayerId = 1 });
            await db.SaveChangesAsync();
        }

        var svc = BuildService(factory);
        await svc.SetInitialRatingAsync(100, 1, rating: 1400, acceptedByUserId: "admin");
        await svc.SetInitialRatingAsync(100, 1, rating: 1550, acceptedByUserId: "admin");

        using var dbCheck = factory.CreateDbContext();
        var snaps = await dbCheck.PlayerRatingSnapshots.ToListAsync();
        Assert.Single(snaps);
        Assert.Equal(1550, snaps[0].Rating);
    }

    [Fact]
    public async Task RosterLookup_PrefersCurrentSeasonStart_OverParent()
    {
        var factory = new TestDbContextFactory();
        using (var db = factory.CreateDbContext())
        {
            db.Players.Add(new Player { PlayerId = 1, DisplayName = "A" });
            db.Competition.Add(new Competition { CompetitionID = 100, CompetitionName = "Parent" });
            db.Competition.Add(new Competition
            {
                CompetitionID = 200, CompetitionName = "Child",
                SeededFromCompetitionId = 100
            });
            db.CompetitionParticipants.Add(new CompetitionParticipant { CompetitionId = 200, PlayerId = 1 });
            // Parent has SeasonEnd=1700, but the admin set a manual override on
            // the current competition — that override must win.
            db.PlayerRatingSnapshots.AddRange(
                new PlayerRatingSnapshot
                {
                    PlayerId = 1, CompetitionId = 100,
                    Kind = PlayerRatingSnapshotKind.SeasonEnd,
                    Rating = 1700, RubbersPlayed = 12, IsProvisional = false
                },
                new PlayerRatingSnapshot
                {
                    PlayerId = 1, CompetitionId = 200,
                    Kind = PlayerRatingSnapshotKind.SeasonStart,
                    Rating = 1500, RubbersPlayed = 0, IsProvisional = false
                });
            await db.SaveChangesAsync();
        }

        var svc = BuildService(factory);
        var ratings = await svc.GetRosterRatingsAsync(competitionId: 200);

        Assert.Equal(1500, ratings[1].Value);
    }

    [Fact]
    public async Task ComputePending_NoParent_ReturnsEmpty()
    {
        var factory = new TestDbContextFactory();
        using (var db = factory.CreateDbContext())
        {
            db.Competition.Add(new Competition { CompetitionID = 100, CompetitionName = "Standalone" });
            await db.SaveChangesAsync();
        }

        var svc = BuildService(factory);
        var suggestions = await svc.ComputePendingSuggestionsAsync(competitionId: 100);

        Assert.Empty(suggestions);
    }

    [Fact]
    public async Task ComputePending_OneRubber_WinnerGainsLoserLoses()
    {
        // P1+P2 (home, ratings 1500/1500) beat P3+P4 (away, 1500/1500). Equal
        // pair averages → expected = 0.5; home wins → home each gain ~20
        // (K=40, score=1.0, delta=40*(1.0-0.5)=20). Away each lose ~20.
        var factory = new TestDbContextFactory();
        using (var db = factory.CreateDbContext())
        {
            db.Players.AddRange(
                new Player { PlayerId = 1, DisplayName = "P1" },
                new Player { PlayerId = 2, DisplayName = "P2" },
                new Player { PlayerId = 3, DisplayName = "P3" },
                new Player { PlayerId = 4, DisplayName = "P4" });
            db.Competition.Add(new Competition { CompetitionID = 100, CompetitionName = "Parent" });
            db.Competition.Add(new Competition
            {
                CompetitionID = 200, CompetitionName = "Child",
                SeededFromCompetitionId = 100
            });
            db.CompetitionParticipants.AddRange(
                new CompetitionParticipant { CompetitionId = 200, PlayerId = 1 },
                new CompetitionParticipant { CompetitionId = 200, PlayerId = 2 },
                new CompetitionParticipant { CompetitionId = 200, PlayerId = 3 },
                new CompetitionParticipant { CompetitionId = 200, PlayerId = 4 });
            db.CompetitionTeams.AddRange(
                new CompetitionTeam { CompetitionTeamId = 10, CompetitionId = 100, Name = "Home" },
                new CompetitionTeam { CompetitionTeamId = 11, CompetitionId = 100, Name = "Away" });
            db.CompetitionFixtures.Add(new CompetitionFixture
            {
                CompetitionFixtureId = 50, CompetitionId = 100,
                HomeTeamId = 10, AwayTeamId = 11, WinnerTeamId = 10,
                Status = DropShot.Shared.FixtureStatus.Completed,
                CompletedAt = new DateTime(2026, 1, 1),
            });
            db.Rubbers.Add(new Rubber
            {
                RubberId = 1, CompetitionFixtureId = 50,
                Name = "R1", Order = 1, CourtNumber = 1,
                HomePlayer1Id = 1, HomePlayer2Id = 2,
                AwayPlayer1Id = 3, AwayPlayer2Id = 4,
                IsComplete = true,
                WinnerTeamId = 10,
            });
            await db.SaveChangesAsync();
        }

        var svc = BuildService(factory);
        var suggestions = (await svc.ComputePendingSuggestionsAsync(competitionId: 200))
            .ToDictionary(s => s.PlayerId);

        Assert.Equal(4, suggestions.Count);
        Assert.Equal(20.0, suggestions[1].Delta, precision: 3);
        Assert.Equal(20.0, suggestions[2].Delta, precision: 3);
        Assert.Equal(-20.0, suggestions[3].Delta, precision: 3);
        Assert.Equal(-20.0, suggestions[4].Delta, precision: 3);
        Assert.Equal(1, suggestions[1].RubbersPlayed);
    }

    [Fact]
    public async Task ComputePending_PlayerDidntPlay_NotInSuggestions()
    {
        var factory = new TestDbContextFactory();
        using (var db = factory.CreateDbContext())
        {
            db.Players.AddRange(
                new Player { PlayerId = 1, DisplayName = "P1" },
                new Player { PlayerId = 2, DisplayName = "P2" },
                new Player { PlayerId = 99, DisplayName = "Spectator" });
            db.Competition.Add(new Competition { CompetitionID = 100, CompetitionName = "Parent" });
            db.Competition.Add(new Competition
            {
                CompetitionID = 200, CompetitionName = "Child",
                SeededFromCompetitionId = 100
            });
            // Player 99 is on the new roster but didn't appear in any rubber.
            db.CompetitionParticipants.AddRange(
                new CompetitionParticipant { CompetitionId = 200, PlayerId = 1 },
                new CompetitionParticipant { CompetitionId = 200, PlayerId = 2 },
                new CompetitionParticipant { CompetitionId = 200, PlayerId = 99 });
            db.CompetitionTeams.AddRange(
                new CompetitionTeam { CompetitionTeamId = 10, CompetitionId = 100, Name = "H" },
                new CompetitionTeam { CompetitionTeamId = 11, CompetitionId = 100, Name = "A" });
            db.CompetitionFixtures.Add(new CompetitionFixture
            {
                CompetitionFixtureId = 50, CompetitionId = 100,
                HomeTeamId = 10, AwayTeamId = 11, WinnerTeamId = 10,
                Status = DropShot.Shared.FixtureStatus.Completed,
            });
            db.Rubbers.Add(new Rubber
            {
                RubberId = 1, CompetitionFixtureId = 50, Name = "R1",
                HomePlayer1Id = 1, AwayPlayer1Id = 2,
                IsComplete = true, WinnerTeamId = 10,
            });
            await db.SaveChangesAsync();
        }

        var svc = BuildService(factory);
        var suggestions = await svc.ComputePendingSuggestionsAsync(competitionId: 200);

        Assert.DoesNotContain(suggestions, s => s.PlayerId == 99);
        Assert.Contains(suggestions, s => s.PlayerId == 1);
        Assert.Contains(suggestions, s => s.PlayerId == 2);
    }

    [Fact]
    public async Task Accept_WritesBothSnapshots_AndIsIdempotent()
    {
        var factory = new TestDbContextFactory();
        using (var db = factory.CreateDbContext())
        {
            db.Players.AddRange(
                new Player { PlayerId = 1, DisplayName = "P1" },
                new Player { PlayerId = 2, DisplayName = "P2" });
            db.Competition.Add(new Competition { CompetitionID = 100, CompetitionName = "Parent" });
            db.Competition.Add(new Competition
            {
                CompetitionID = 200, CompetitionName = "Child",
                SeededFromCompetitionId = 100
            });
            db.CompetitionParticipants.AddRange(
                new CompetitionParticipant { CompetitionId = 200, PlayerId = 1 },
                new CompetitionParticipant { CompetitionId = 200, PlayerId = 2 });
            db.CompetitionTeams.AddRange(
                new CompetitionTeam { CompetitionTeamId = 10, CompetitionId = 100, Name = "H" },
                new CompetitionTeam { CompetitionTeamId = 11, CompetitionId = 100, Name = "A" });
            db.CompetitionFixtures.Add(new CompetitionFixture
            {
                CompetitionFixtureId = 50, CompetitionId = 100,
                HomeTeamId = 10, AwayTeamId = 11, WinnerTeamId = 10,
                Status = DropShot.Shared.FixtureStatus.Completed,
            });
            db.Rubbers.Add(new Rubber
            {
                RubberId = 1, CompetitionFixtureId = 50, Name = "R1",
                HomePlayer1Id = 1, AwayPlayer1Id = 2,
                IsComplete = true, WinnerTeamId = 10,
            });
            await db.SaveChangesAsync();
        }

        var svc = BuildService(factory);
        var applied1 = await svc.AcceptAllSuggestionsAsync(competitionId: 200, acceptedByUserId: "admin");
        Assert.Equal(2, applied1.Count);

        using (var dbCheck = factory.CreateDbContext())
        {
            // Two snapshots per player: SeasonEnd on parent (100), SeasonStart on child (200).
            Assert.Equal(4, await dbCheck.PlayerRatingSnapshots.CountAsync());
            var p1Start = await dbCheck.PlayerRatingSnapshots.SingleAsync(s =>
                s.PlayerId == 1 && s.CompetitionId == 200 && s.Kind == PlayerRatingSnapshotKind.SeasonStart);
            var p1End = await dbCheck.PlayerRatingSnapshots.SingleAsync(s =>
                s.PlayerId == 1 && s.CompetitionId == 100 && s.Kind == PlayerRatingSnapshotKind.SeasonEnd);
            Assert.Equal(p1Start.Rating, p1End.Rating);
        }

        // Idempotency: a second Accept-All over the same data must NOT create
        // duplicate rows. After Accept the suggestions are filtered out by
        // GetVisibleSuggestionsAsync, but ComputePendingSuggestionsAsync still
        // returns the same numbers, and the upsert path keeps row count flat.
        var applied2 = await svc.AcceptAllSuggestionsAsync(competitionId: 200, acceptedByUserId: "admin");
        Assert.Equal(2, applied2.Count);
        using (var dbCheck = factory.CreateDbContext())
        {
            Assert.Equal(4, await dbCheck.PlayerRatingSnapshots.CountAsync());
        }
    }

    [Fact]
    public async Task GetVisibleSuggestions_HidesAlreadyApplied()
    {
        var factory = new TestDbContextFactory();
        using (var db = factory.CreateDbContext())
        {
            db.Players.AddRange(
                new Player { PlayerId = 1, DisplayName = "P1" },
                new Player { PlayerId = 2, DisplayName = "P2" });
            db.Competition.Add(new Competition { CompetitionID = 100, CompetitionName = "Parent" });
            db.Competition.Add(new Competition
            {
                CompetitionID = 200, CompetitionName = "Child",
                SeededFromCompetitionId = 100
            });
            db.CompetitionParticipants.AddRange(
                new CompetitionParticipant { CompetitionId = 200, PlayerId = 1 },
                new CompetitionParticipant { CompetitionId = 200, PlayerId = 2 });
            db.CompetitionTeams.AddRange(
                new CompetitionTeam { CompetitionTeamId = 10, CompetitionId = 100, Name = "H" },
                new CompetitionTeam { CompetitionTeamId = 11, CompetitionId = 100, Name = "A" });
            db.CompetitionFixtures.Add(new CompetitionFixture
            {
                CompetitionFixtureId = 50, CompetitionId = 100,
                HomeTeamId = 10, AwayTeamId = 11, WinnerTeamId = 10,
                Status = DropShot.Shared.FixtureStatus.Completed,
            });
            db.Rubbers.Add(new Rubber
            {
                RubberId = 1, CompetitionFixtureId = 50, Name = "R1",
                HomePlayer1Id = 1, AwayPlayer1Id = 2,
                IsComplete = true, WinnerTeamId = 10,
            });
            await db.SaveChangesAsync();
        }

        var svc = BuildService(factory);
        // Before any Accept: both players see a suggestion.
        var before = await svc.GetVisibleSuggestionsAsync(competitionId: 200);
        Assert.Equal(2, before.Count);

        // Accept for P1 only.
        await svc.AcceptSuggestionAsync(competitionId: 200, playerId: 1, acceptedByUserId: "admin");

        var after = await svc.GetVisibleSuggestionsAsync(competitionId: 200);
        Assert.Single(after);
        Assert.Equal(2, after[0].PlayerId);
    }

    [Fact]
    public async Task SuggestDivisions_PartitionsByRating()
    {
        var factory = new TestDbContextFactory();
        using (var db = factory.CreateDbContext())
        {
            db.Players.AddRange(
                Enumerable.Range(1, 6).Select(i => new Player { PlayerId = i, DisplayName = $"P{i}" }));
            db.Competition.Add(new Competition { CompetitionID = 100, CompetitionName = "C" });
            db.CompetitionDivisions.AddRange(
                new CompetitionDivision { CompetitionDivisionId = 1, CompetitionId = 100, Rank = 1, Name = "Top" },
                new CompetitionDivision { CompetitionDivisionId = 2, CompetitionId = 100, Rank = 2, Name = "Mid" },
                new CompetitionDivision { CompetitionDivisionId = 3, CompetitionId = 100, Rank = 3, Name = "Bot" });
            db.CompetitionParticipants.AddRange(
                new CompetitionParticipant { CompetitionId = 100, PlayerId = 1, CompetitionDivisionId = 3 },
                new CompetitionParticipant { CompetitionId = 100, PlayerId = 2 },
                new CompetitionParticipant { CompetitionId = 100, PlayerId = 3 },
                new CompetitionParticipant { CompetitionId = 100, PlayerId = 4 },
                new CompetitionParticipant { CompetitionId = 100, PlayerId = 5 },
                new CompetitionParticipant { CompetitionId = 100, PlayerId = 6 });
            // No parent → use manual SeasonStart ratings on this competition.
            db.PlayerRatingSnapshots.AddRange(
                new PlayerRatingSnapshot { PlayerId = 1, CompetitionId = 100, Kind = PlayerRatingSnapshotKind.SeasonStart, Rating = 1800 },
                new PlayerRatingSnapshot { PlayerId = 2, CompetitionId = 100, Kind = PlayerRatingSnapshotKind.SeasonStart, Rating = 1700 },
                new PlayerRatingSnapshot { PlayerId = 3, CompetitionId = 100, Kind = PlayerRatingSnapshotKind.SeasonStart, Rating = 1600 },
                new PlayerRatingSnapshot { PlayerId = 4, CompetitionId = 100, Kind = PlayerRatingSnapshotKind.SeasonStart, Rating = 1500 },
                new PlayerRatingSnapshot { PlayerId = 5, CompetitionId = 100, Kind = PlayerRatingSnapshotKind.SeasonStart, Rating = 1400 },
                new PlayerRatingSnapshot { PlayerId = 6, CompetitionId = 100, Kind = PlayerRatingSnapshotKind.SeasonStart, Rating = 1300 });
            await db.SaveChangesAsync();
        }

        var svc = BuildService(factory);
        var placements = (await svc.SuggestDivisionPlacementsAsync(competitionId: 100))
            .ToDictionary(p => p.PlayerId);

        // 6 players / 3 divisions → 2 each. P1+P2 top, P3+P4 mid, P5+P6 bot.
        Assert.Equal(1, placements[1].SuggestedDivisionId);
        Assert.Equal(1, placements[2].SuggestedDivisionId);
        Assert.Equal(2, placements[3].SuggestedDivisionId);
        Assert.Equal(2, placements[4].SuggestedDivisionId);
        Assert.Equal(3, placements[5].SuggestedDivisionId);
        // P6 is suggested to stay in Bot (3); but P6 currently has no division
        // assigned, so the suggestion IS returned.
        // P1 was in Bot (3), suggested to move to Top (1).

        // Sanity: anyone already in the right division shouldn't appear.
        // P1 was placed in 3 but suggested 1 — appears.
        Assert.True(placements.ContainsKey(1));
    }

    [Fact]
    public async Task SuggestDivisions_NoRatings_DefaultsAllPlayersAndPartitions()
    {
        // With everyone at the default 1500, partitioning is by secondary
        // sort (PlayerId asc). Critically, the engine must still produce a
        // suggestion for every participant whose current division differs —
        // otherwise the "Apply all placement suggestions" button never appears
        // on a brand-new roster.
        var factory = new TestDbContextFactory();
        using (var db = factory.CreateDbContext())
        {
            db.Players.AddRange(
                new Player { PlayerId = 1, DisplayName = "P1" },
                new Player { PlayerId = 2, DisplayName = "P2" },
                new Player { PlayerId = 3, DisplayName = "P3" },
                new Player { PlayerId = 4, DisplayName = "P4" });
            db.Competition.Add(new Competition { CompetitionID = 100, CompetitionName = "C" });
            db.CompetitionDivisions.AddRange(
                new CompetitionDivision { CompetitionDivisionId = 1, CompetitionId = 100, Rank = 1, Name = "A" },
                new CompetitionDivision { CompetitionDivisionId = 2, CompetitionId = 100, Rank = 2, Name = "B" });
            db.CompetitionParticipants.AddRange(
                new CompetitionParticipant { CompetitionId = 100, PlayerId = 1 },
                new CompetitionParticipant { CompetitionId = 100, PlayerId = 2 },
                new CompetitionParticipant { CompetitionId = 100, PlayerId = 3 },
                new CompetitionParticipant { CompetitionId = 100, PlayerId = 4 });
            await db.SaveChangesAsync();
        }

        var svc = BuildService(factory);
        var placements = (await svc.SuggestDivisionPlacementsAsync(competitionId: 100))
            .ToDictionary(p => p.PlayerId);

        // 4 players / 2 divisions → 2 each. PlayerId ascending breaks the
        // all-equal tie, so P1+P2 land in Div 1, P3+P4 in Div 2.
        Assert.Equal(4, placements.Count);
        Assert.Equal(1, placements[1].SuggestedDivisionId);
        Assert.Equal(1, placements[2].SuggestedDivisionId);
        Assert.Equal(2, placements[3].SuggestedDivisionId);
        Assert.Equal(2, placements[4].SuggestedDivisionId);
    }

    [Fact]
    public async Task SuggestRoles_AssignsHigherRatedToSlotA()
    {
        var factory = new TestDbContextFactory();
        using (var db = factory.CreateDbContext())
        {
            db.Players.AddRange(
                new Player { PlayerId = 1, DisplayName = "M-Strong", Sex = PlayerSex.Male },
                new Player { PlayerId = 2, DisplayName = "M-Weak",   Sex = PlayerSex.Male },
                new Player { PlayerId = 3, DisplayName = "F-Strong", Sex = PlayerSex.Female },
                new Player { PlayerId = 4, DisplayName = "F-Weak",   Sex = PlayerSex.Female });
            db.Competition.Add(new Competition
            {
                CompetitionID = 100, CompetitionName = "C",
                CompetitionFormat = CompetitionFormat.TeamMatch
            });
            db.CompetitionTeams.Add(new CompetitionTeam
            {
                CompetitionTeamId = 10, CompetitionId = 100, Name = "T1"
            });
            // Members in the WRONG starting roles to verify the assigner moves them.
            db.CompetitionParticipants.AddRange(
                new CompetitionParticipant { CompetitionId = 100, PlayerId = 1, TeamId = 10, Role = "MB" },
                new CompetitionParticipant { CompetitionId = 100, PlayerId = 2, TeamId = 10, Role = "MA" },
                new CompetitionParticipant { CompetitionId = 100, PlayerId = 3, TeamId = 10, Role = "FB" },
                new CompetitionParticipant { CompetitionId = 100, PlayerId = 4, TeamId = 10, Role = "FA" });
            db.PlayerRatingSnapshots.AddRange(
                new PlayerRatingSnapshot { PlayerId = 1, CompetitionId = 100, Kind = PlayerRatingSnapshotKind.SeasonStart, Rating = 1700 },
                new PlayerRatingSnapshot { PlayerId = 2, CompetitionId = 100, Kind = PlayerRatingSnapshotKind.SeasonStart, Rating = 1400 },
                new PlayerRatingSnapshot { PlayerId = 3, CompetitionId = 100, Kind = PlayerRatingSnapshotKind.SeasonStart, Rating = 1650 },
                new PlayerRatingSnapshot { PlayerId = 4, CompetitionId = 100, Kind = PlayerRatingSnapshotKind.SeasonStart, Rating = 1450 });
            await db.SaveChangesAsync();
        }

        var svc = BuildService(factory);
        var placements = (await svc.SuggestRolePlacementsAsync(competitionId: 100))
            .ToDictionary(p => p.PlayerId);

        // Higher-rated male (P1) → MA; lower (P2) → MB. Same for females.
        Assert.Equal("MA", placements[1].SuggestedRole);
        Assert.Equal("MB", placements[2].SuggestedRole);
        Assert.Equal("FA", placements[3].SuggestedRole);
        Assert.Equal("FB", placements[4].SuggestedRole);
    }

    [Fact]
    public async Task SuggestRoles_UnteamedPlayers_StillGetSuggestions()
    {
        // Players don't have to be on a team yet — role assignment is per
        // division, not per team — so the admin can role-balance the pool
        // before generating teams.
        var factory = new TestDbContextFactory();
        using (var db = factory.CreateDbContext())
        {
            db.Players.AddRange(
                new Player { PlayerId = 1, DisplayName = "M-Strong", Sex = PlayerSex.Male },
                new Player { PlayerId = 2, DisplayName = "M-Weak",   Sex = PlayerSex.Male },
                new Player { PlayerId = 3, DisplayName = "F-Strong", Sex = PlayerSex.Female },
                new Player { PlayerId = 4, DisplayName = "F-Weak",   Sex = PlayerSex.Female });
            db.Competition.Add(new Competition
            {
                CompetitionID = 100, CompetitionName = "C",
                CompetitionFormat = CompetitionFormat.TeamMatch
            });
            // No teams. Participants have no TeamId.
            db.CompetitionParticipants.AddRange(
                new CompetitionParticipant { CompetitionId = 100, PlayerId = 1 },
                new CompetitionParticipant { CompetitionId = 100, PlayerId = 2 },
                new CompetitionParticipant { CompetitionId = 100, PlayerId = 3 },
                new CompetitionParticipant { CompetitionId = 100, PlayerId = 4 });
            db.PlayerRatingSnapshots.AddRange(
                new PlayerRatingSnapshot { PlayerId = 1, CompetitionId = 100, Kind = PlayerRatingSnapshotKind.SeasonStart, Rating = 1700 },
                new PlayerRatingSnapshot { PlayerId = 2, CompetitionId = 100, Kind = PlayerRatingSnapshotKind.SeasonStart, Rating = 1400 },
                new PlayerRatingSnapshot { PlayerId = 3, CompetitionId = 100, Kind = PlayerRatingSnapshotKind.SeasonStart, Rating = 1650 },
                new PlayerRatingSnapshot { PlayerId = 4, CompetitionId = 100, Kind = PlayerRatingSnapshotKind.SeasonStart, Rating = 1450 });
            await db.SaveChangesAsync();
        }

        var svc = BuildService(factory);
        var placements = (await svc.SuggestRolePlacementsAsync(competitionId: 100))
            .ToDictionary(p => p.PlayerId);

        Assert.Equal("MA", placements[1].SuggestedRole);
        Assert.Equal("MB", placements[2].SuggestedRole);
        Assert.Equal("FA", placements[3].SuggestedRole);
        Assert.Equal("FB", placements[4].SuggestedRole);
    }

    [Fact]
    public async Task SuggestRoles_PerDivision_AssignedSeparately()
    {
        // Two divisions, each with its own MA/MB split. P1+P2 in Div 1
        // (one MA, one MB regardless of cross-division ratings); P3+P4
        // in Div 2 (same).
        var factory = new TestDbContextFactory();
        using (var db = factory.CreateDbContext())
        {
            db.Players.AddRange(
                new Player { PlayerId = 1, DisplayName = "Div1-Strong", Sex = PlayerSex.Male },
                new Player { PlayerId = 2, DisplayName = "Div1-Weak",   Sex = PlayerSex.Male },
                new Player { PlayerId = 3, DisplayName = "Div2-Strong", Sex = PlayerSex.Male },
                new Player { PlayerId = 4, DisplayName = "Div2-Weak",   Sex = PlayerSex.Male });
            db.Competition.Add(new Competition
            {
                CompetitionID = 100, CompetitionName = "C",
                CompetitionFormat = CompetitionFormat.TeamMatch,
                HasDivisions = true,
            });
            db.CompetitionDivisions.AddRange(
                new CompetitionDivision { CompetitionDivisionId = 1, CompetitionId = 100, Rank = 1, Name = "Top" },
                new CompetitionDivision { CompetitionDivisionId = 2, CompetitionId = 100, Rank = 2, Name = "Bot" });
            db.CompetitionParticipants.AddRange(
                new CompetitionParticipant { CompetitionId = 100, PlayerId = 1, CompetitionDivisionId = 1 },
                new CompetitionParticipant { CompetitionId = 100, PlayerId = 2, CompetitionDivisionId = 1 },
                new CompetitionParticipant { CompetitionId = 100, PlayerId = 3, CompetitionDivisionId = 2 },
                new CompetitionParticipant { CompetitionId = 100, PlayerId = 4, CompetitionDivisionId = 2 });
            // Cross-division ratings: P3 (Div 2) is rated higher than P2 (Div 1)
            // — proves that role assignment is scoped by division, not global.
            db.PlayerRatingSnapshots.AddRange(
                new PlayerRatingSnapshot { PlayerId = 1, CompetitionId = 100, Kind = PlayerRatingSnapshotKind.SeasonStart, Rating = 1900 },
                new PlayerRatingSnapshot { PlayerId = 2, CompetitionId = 100, Kind = PlayerRatingSnapshotKind.SeasonStart, Rating = 1400 },
                new PlayerRatingSnapshot { PlayerId = 3, CompetitionId = 100, Kind = PlayerRatingSnapshotKind.SeasonStart, Rating = 1700 },
                new PlayerRatingSnapshot { PlayerId = 4, CompetitionId = 100, Kind = PlayerRatingSnapshotKind.SeasonStart, Rating = 1200 });
            await db.SaveChangesAsync();
        }

        var svc = BuildService(factory);
        var placements = (await svc.SuggestRolePlacementsAsync(competitionId: 100))
            .ToDictionary(p => p.PlayerId);

        // Div 1: P1 > P2 → P1 MA, P2 MB.
        // Div 2: P3 > P4 → P3 MA, P4 MB. P3 gets MA despite being weaker than P1.
        Assert.Equal("MA", placements[1].SuggestedRole);
        Assert.Equal("MB", placements[2].SuggestedRole);
        Assert.Equal("MA", placements[3].SuggestedRole);
        Assert.Equal("MB", placements[4].SuggestedRole);
    }

    [Fact]
    public async Task SuggestRoles_OddCount_GivesExtraToASlot()
    {
        // 3 males in a single (no-division) pool: top 2 → MA, bottom 1 → MB.
        var factory = new TestDbContextFactory();
        using (var db = factory.CreateDbContext())
        {
            db.Players.AddRange(
                new Player { PlayerId = 1, DisplayName = "Strong", Sex = PlayerSex.Male },
                new Player { PlayerId = 2, DisplayName = "Mid",    Sex = PlayerSex.Male },
                new Player { PlayerId = 3, DisplayName = "Weak",   Sex = PlayerSex.Male });
            db.Competition.Add(new Competition
            {
                CompetitionID = 100, CompetitionName = "C",
                CompetitionFormat = CompetitionFormat.TeamMatch,
            });
            db.CompetitionParticipants.AddRange(
                new CompetitionParticipant { CompetitionId = 100, PlayerId = 1 },
                new CompetitionParticipant { CompetitionId = 100, PlayerId = 2 },
                new CompetitionParticipant { CompetitionId = 100, PlayerId = 3 });
            db.PlayerRatingSnapshots.AddRange(
                new PlayerRatingSnapshot { PlayerId = 1, CompetitionId = 100, Kind = PlayerRatingSnapshotKind.SeasonStart, Rating = 1800 },
                new PlayerRatingSnapshot { PlayerId = 2, CompetitionId = 100, Kind = PlayerRatingSnapshotKind.SeasonStart, Rating = 1500 },
                new PlayerRatingSnapshot { PlayerId = 3, CompetitionId = 100, Kind = PlayerRatingSnapshotKind.SeasonStart, Rating = 1200 });
            await db.SaveChangesAsync();
        }

        var svc = BuildService(factory);
        var placements = (await svc.SuggestRolePlacementsAsync(competitionId: 100))
            .ToDictionary(p => p.PlayerId);

        Assert.Equal("MA", placements[1].SuggestedRole);
        Assert.Equal("MA", placements[2].SuggestedRole);
        Assert.Equal("MB", placements[3].SuggestedRole);
    }

    [Fact]
    public void AssignMttByRating_DegradesToAlphabetical_WhenNoRatings()
    {
        // When no ratings are set, ordering must fall back to DisplayName so
        // the registry-default behavior matches AssignMtt.
        var assigner = DropShot.Shared.RubberTemplateRegistry.GetRoleAssigner(
            DropShot.Shared.RubberTemplateRegistry.MttRatedKey);
        Assert.NotNull(assigner);

        var candidates = new List<DropShot.Shared.RubberTemplateRegistry.AssignmentCandidate>
        {
            new(1, "Bob",     PlayerSex.Male,   Rating: null),
            new(2, "Alice",   PlayerSex.Male,   Rating: null),
            new(3, "Dana",    PlayerSex.Female, Rating: null),
            new(4, "Charlie", PlayerSex.Female, Rating: null),
        };
        var result = assigner!(candidates);
        Assert.Equal("MA", result[2]); // Alice (alphabetically first male) → MA
        Assert.Equal("MB", result[1]); // Bob → MB
        Assert.Equal("FA", result[4]); // Charlie → FA
        Assert.Equal("FB", result[3]); // Dana → FB
    }

    [Fact]
    public async Task SuggestRoles_PartialRatings_StillAssignsUsingDefault()
    {
        // Only P1 has an explicit rating; P2 takes the 1500 default. The
        // engine must still produce role suggestions for the team — the
        // rating-aware assigner sorts P1 (1700) above P2 (1500), so the
        // higher-rated male gets MA.
        var factory = new TestDbContextFactory();
        using (var db = factory.CreateDbContext())
        {
            db.Players.AddRange(
                new Player { PlayerId = 1, DisplayName = "M1", Sex = PlayerSex.Male },
                new Player { PlayerId = 2, DisplayName = "M2", Sex = PlayerSex.Male });
            db.Competition.Add(new Competition
            {
                CompetitionID = 100, CompetitionName = "C",
                CompetitionFormat = CompetitionFormat.TeamMatch
            });
            db.CompetitionTeams.Add(new CompetitionTeam
            {
                CompetitionTeamId = 10, CompetitionId = 100, Name = "T1"
            });
            db.CompetitionParticipants.AddRange(
                new CompetitionParticipant { CompetitionId = 100, PlayerId = 1, TeamId = 10, Role = "MB" },
                new CompetitionParticipant { CompetitionId = 100, PlayerId = 2, TeamId = 10, Role = "MA" });
            db.PlayerRatingSnapshots.Add(new PlayerRatingSnapshot
            {
                PlayerId = 1, CompetitionId = 100, Kind = PlayerRatingSnapshotKind.SeasonStart, Rating = 1700
            });
            await db.SaveChangesAsync();
        }

        var svc = BuildService(factory);
        var placements = (await svc.SuggestRolePlacementsAsync(competitionId: 100))
            .ToDictionary(p => p.PlayerId);

        Assert.Equal("MA", placements[1].SuggestedRole);
        Assert.Equal("MB", placements[2].SuggestedRole);
    }

    [Fact]
    public async Task BulkLoad_ReturnsRatingsForAllParticipants()
    {
        var factory = new TestDbContextFactory();
        using (var db = factory.CreateDbContext())
        {
            db.Players.AddRange(
                new Player { PlayerId = 1, DisplayName = "A" },
                new Player { PlayerId = 2, DisplayName = "B" },
                new Player { PlayerId = 3, DisplayName = "C" });
            db.Competition.Add(new Competition { CompetitionID = 100, CompetitionName = "Parent" });
            db.Competition.Add(new Competition
            {
                CompetitionID = 200, CompetitionName = "Child",
                SeededFromCompetitionId = 100
            });
            db.CompetitionParticipants.AddRange(
                new CompetitionParticipant { CompetitionId = 200, PlayerId = 1 },
                new CompetitionParticipant { CompetitionId = 200, PlayerId = 2 },
                new CompetitionParticipant { CompetitionId = 200, PlayerId = 3 });
            db.PlayerRatingSnapshots.AddRange(
                new PlayerRatingSnapshot
                {
                    PlayerId = 1, CompetitionId = 100,
                    Kind = PlayerRatingSnapshotKind.SeasonEnd,
                    Rating = 1600, RubbersPlayed = 12, IsProvisional = false
                },
                new PlayerRatingSnapshot
                {
                    PlayerId = 2, CompetitionId = 100,
                    Kind = PlayerRatingSnapshotKind.SeasonStart,
                    Rating = 1450, RubbersPlayed = 0, IsProvisional = true
                });
            await db.SaveChangesAsync();
        }

        var svc = BuildService(factory);
        var ratings = await svc.GetCurrentRatingsForCompetitionAsync(competitionId: 200);

        Assert.True(ratings.ContainsKey(1));
        Assert.Equal(1600, ratings[1].Value);
        Assert.False(ratings[1].IsProvisional);

        Assert.True(ratings.ContainsKey(2));
        Assert.Equal(1450, ratings[2].Value);
        Assert.True(ratings[2].IsProvisional);

        // Player 3 has no snapshot — bulk-load omits them so the roster falls
        // back to the per-row null which renders as em-dash.
        Assert.False(ratings.ContainsKey(3));
    }
}
