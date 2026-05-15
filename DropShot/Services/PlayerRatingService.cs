using DropShot.Data;
using DropShot.Models;
using DropShot.Shared;
using Microsoft.EntityFrameworkCore;

namespace DropShot.Services;

/// <summary>
/// Reads / resolves player Elo ratings scoped to a competition lineage (chain of
/// competitions linked via <see cref="Competition.SeededFromCompetitionId"/>).
/// Snapshots are written by the per-season Apply flow (slice 2); slice 1 only
/// reads them.
/// </summary>
public sealed class PlayerRatingService(IDbContextFactory<MyDbContext> dbFactory)
{
    public const double DefaultRating = 1500.0;
    public const int ProvisionalThreshold = 10;

    public record Rating(double Value, bool IsProvisional);

    /// <summary>
    /// Resolve the rating that player should bring into <paramref name="competitionId"/>.
    /// Walks <c>SeededFromCompetitionId</c> exactly once — does not recurse further
    /// back into the lineage. Order of preference on the parent competition:
    /// 1. SeasonEnd snapshot (player completed the parent season and was accepted)
    /// 2. SeasonStart snapshot (player carried a rating into the parent but didn't play)
    /// 3. Default rating (1500, provisional)
    /// </summary>
    public async Task<Rating> GetCurrentRatingAsync(int playerId, int competitionId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var parentId = await db.Competition
            .AsNoTracking()
            .Where(c => c.CompetitionID == competitionId)
            .Select(c => c.SeededFromCompetitionId)
            .FirstOrDefaultAsync(ct);

        if (parentId is null) return new Rating(DefaultRating, IsProvisional: true);

        var parentSnapshots = await db.PlayerRatingSnapshots
            .AsNoTracking()
            .Where(s => s.CompetitionId == parentId && s.PlayerId == playerId)
            .ToListAsync(ct);

        var seasonEnd = parentSnapshots.FirstOrDefault(s => s.Kind == PlayerRatingSnapshotKind.SeasonEnd);
        if (seasonEnd is not null) return new Rating(seasonEnd.Rating, seasonEnd.IsProvisional);

        var seasonStart = parentSnapshots.FirstOrDefault(s => s.Kind == PlayerRatingSnapshotKind.SeasonStart);
        if (seasonStart is not null) return new Rating(seasonStart.Rating, seasonStart.IsProvisional);

        return new Rating(DefaultRating, IsProvisional: true);
    }

    /// <summary>
    /// Bulk-load all snapshots for every participant of <paramref name="competitionId"/>,
    /// resolved against the parent competition. Used by the roster page to render
    /// each row's "current rating" without per-row queries. Returns a map of
    /// PlayerId → resolved Rating.
    /// </summary>
    public async Task<Dictionary<int, Rating>> GetCurrentRatingsForCompetitionAsync(
        int competitionId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var comp = await db.Competition
            .AsNoTracking()
            .Where(c => c.CompetitionID == competitionId)
            .Select(c => new { c.CompetitionID, c.SeededFromCompetitionId })
            .FirstOrDefaultAsync(ct);
        if (comp is null) return new Dictionary<int, Rating>();

        var participantIds = await db.CompetitionParticipants
            .AsNoTracking()
            .Where(p => p.CompetitionId == competitionId)
            .Select(p => p.PlayerId)
            .ToListAsync(ct);

        if (participantIds.Count == 0 || comp.SeededFromCompetitionId is null)
            return new Dictionary<int, Rating>();

        var parentId = comp.SeededFromCompetitionId.Value;
        var snapshots = await db.PlayerRatingSnapshots
            .AsNoTracking()
            .Where(s => s.CompetitionId == parentId && participantIds.Contains(s.PlayerId))
            .ToListAsync(ct);

        var result = new Dictionary<int, Rating>(participantIds.Count);
        foreach (var playerId in participantIds)
        {
            var forPlayer = snapshots.Where(s => s.PlayerId == playerId).ToList();
            var seasonEnd = forPlayer.FirstOrDefault(s => s.Kind == PlayerRatingSnapshotKind.SeasonEnd);
            if (seasonEnd is not null)
            {
                result[playerId] = new Rating(seasonEnd.Rating, seasonEnd.IsProvisional);
                continue;
            }
            var seasonStart = forPlayer.FirstOrDefault(s => s.Kind == PlayerRatingSnapshotKind.SeasonStart);
            if (seasonStart is not null)
            {
                result[playerId] = new Rating(seasonStart.Rating, seasonStart.IsProvisional);
            }
        }
        return result;
    }

    /// <summary>
    /// Bulk-load that also looks at <c>SeasonStart</c> snapshots on the current
    /// competition itself — used by the roster after an admin has typed initial
    /// ratings inline, since those write a SeasonStart on the current comp (not
    /// the parent). Resolution order per player: current SeasonStart → parent
    /// SeasonEnd → parent SeasonStart → omit.
    /// </summary>
    public async Task<Dictionary<int, Rating>> GetRosterRatingsAsync(
        int competitionId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var comp = await db.Competition
            .AsNoTracking()
            .Where(c => c.CompetitionID == competitionId)
            .Select(c => new { c.CompetitionID, c.SeededFromCompetitionId })
            .FirstOrDefaultAsync(ct);
        if (comp is null) return new Dictionary<int, Rating>();

        var participantIds = await db.CompetitionParticipants
            .AsNoTracking()
            .Where(p => p.CompetitionId == competitionId)
            .Select(p => p.PlayerId)
            .ToListAsync(ct);
        if (participantIds.Count == 0) return new Dictionary<int, Rating>();

        var compIdsToCheck = new List<int> { competitionId };
        if (comp.SeededFromCompetitionId is int parentId) compIdsToCheck.Add(parentId);

        var snapshots = await db.PlayerRatingSnapshots
            .AsNoTracking()
            .Where(s => compIdsToCheck.Contains(s.CompetitionId) && participantIds.Contains(s.PlayerId))
            .ToListAsync(ct);

        var result = new Dictionary<int, Rating>(participantIds.Count);
        foreach (var playerId in participantIds)
        {
            var forPlayer = snapshots.Where(s => s.PlayerId == playerId).ToList();

            var currentStart = forPlayer.FirstOrDefault(s =>
                s.CompetitionId == competitionId && s.Kind == PlayerRatingSnapshotKind.SeasonStart);
            if (currentStart is not null)
            {
                result[playerId] = new Rating(currentStart.Rating, currentStart.IsProvisional);
                continue;
            }
            if (comp.SeededFromCompetitionId is null) continue;

            var parentEnd = forPlayer.FirstOrDefault(s =>
                s.CompetitionId == comp.SeededFromCompetitionId && s.Kind == PlayerRatingSnapshotKind.SeasonEnd);
            if (parentEnd is not null)
            {
                result[playerId] = new Rating(parentEnd.Rating, parentEnd.IsProvisional);
                continue;
            }
            var parentStart = forPlayer.FirstOrDefault(s =>
                s.CompetitionId == comp.SeededFromCompetitionId && s.Kind == PlayerRatingSnapshotKind.SeasonStart);
            if (parentStart is not null)
            {
                result[playerId] = new Rating(parentStart.Rating, parentStart.IsProvisional);
            }
        }
        return result;
    }

    public record PendingSuggestion(
        int PlayerId,
        double PreviousRating,
        double SuggestedRating,
        double Delta,
        int RubbersPlayed);

    /// <summary>
    /// Suggestions visible to the admin: <see cref="ComputePendingSuggestionsAsync"/>
    /// minus any player who already has a <c>SeasonStart</c> snapshot on the
    /// current competition (manually-entered initial rating, or a previously
    /// accepted suggestion). The roster page uses this; an admin who has
    /// already applied a suggestion doesn't see the same arrow again.
    /// </summary>
    public async Task<IReadOnlyList<PendingSuggestion>> GetVisibleSuggestionsAsync(
        int competitionId, CancellationToken ct = default)
    {
        var raw = await ComputePendingSuggestionsAsync(competitionId, ct);
        if (raw.Count == 0) return raw;

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var applied = await db.PlayerRatingSnapshots
            .AsNoTracking()
            .Where(s => s.CompetitionId == competitionId
                     && s.Kind == PlayerRatingSnapshotKind.SeasonStart)
            .Select(s => s.PlayerId)
            .ToListAsync(ct);
        if (applied.Count == 0) return raw;
        var appliedSet = applied.ToHashSet();
        return raw.Where(s => !appliedSet.Contains(s.PlayerId)).ToList();
    }

    /// <summary>
    /// Replays the parent competition's completed rubbers through Elo to derive
    /// each participant's suggested post-season rating. Returns one entry per
    /// player who actually played rubbers in the parent. Players with no parent
    /// or no rubbers played are omitted.
    /// </summary>
    public async Task<IReadOnlyList<PendingSuggestion>> ComputePendingSuggestionsAsync(
        int competitionId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var comp = await db.Competition
            .AsNoTracking()
            .Where(c => c.CompetitionID == competitionId)
            .Select(c => new { c.CompetitionID, c.SeededFromCompetitionId })
            .FirstOrDefaultAsync(ct);
        if (comp?.SeededFromCompetitionId is not int parentId) return Array.Empty<PendingSuggestion>();

        var participantIds = await db.CompetitionParticipants
            .AsNoTracking()
            .Where(p => p.CompetitionId == competitionId)
            .Select(p => p.PlayerId)
            .ToListAsync(ct);
        if (participantIds.Count == 0) return Array.Empty<PendingSuggestion>();

        var preRatings = new Dictionary<int, double>();
        foreach (var pid in participantIds)
        {
            preRatings[pid] = (await GetCurrentRatingAsync(pid, parentId, ct)).Value;
        }

        // Rubbers, ordered chronologically as best we can. CompletedAt is the
        // truthiest signal; fall back to scheduled time, then fixture id, then
        // rubber id for determinism.
        var rubbers = await db.Rubbers
            .AsNoTracking()
            .Include(r => r.Fixture)
            .Where(r => r.IsComplete
                     && r.Fixture.HomeTeamId.HasValue
                     && r.Fixture.AwayTeamId.HasValue
                     && r.Fixture.CompetitionId == parentId)
            .ToListAsync(ct);

        var ordered = rubbers
            .OrderBy(r => r.Fixture.CompletedAt ?? r.Fixture.ScheduledAt ?? DateTime.MaxValue)
            .ThenBy(r => r.CompetitionFixtureId)
            .ThenBy(r => r.RubberId)
            .ToList();

        var current = new Dictionary<int, double>(preRatings);
        var played = new Dictionary<int, int>();

        foreach (var r in ordered)
        {
            var home = new[] { r.HomePlayer1Id, r.HomePlayer2Id }
                .Where(id => id.HasValue).Select(id => id!.Value).ToList();
            var away = new[] { r.AwayPlayer1Id, r.AwayPlayer2Id }
                .Where(id => id.HasValue).Select(id => id!.Value).ToList();
            if (home.Count == 0 || away.Count == 0) continue;

            // Use the rating of each side as the pair's mean. Players outside
            // the current competition's participants (e.g. opponents from the
            // parent who didn't roster again) still need a rating to score
            // against — pull a parent snapshot or default.
            double SideRating(IEnumerable<int> ids) =>
                ids.Average(id =>
                {
                    if (current.TryGetValue(id, out var v)) return v;
                    var fallback = preRatings.TryGetValue(id, out var f) ? f : DefaultRating;
                    return fallback;
                });

            var homeRating = SideRating(home);
            var awayRating = SideRating(away);
            var expectedHome = EloCalculator.ExpectedScore(homeRating, awayRating);
            var expectedAway = 1.0 - expectedHome;

            bool homeWon = r.WinnerTeamId == r.Fixture.HomeTeamId;
            bool awayWon = r.WinnerTeamId == r.Fixture.AwayTeamId;
            if (!homeWon && !awayWon) continue; // no recorded winner — skip

            double scoreHome = homeWon ? 1.0 : 0.0;
            double scoreAway = 1.0 - scoreHome;

            void UpdateOne(int pid, double sideRating, double opponentRating, double expected, double score)
            {
                if (!current.ContainsKey(pid)) current[pid] = preRatings.GetValueOrDefault(pid, DefaultRating);
                played.TryGetValue(pid, out var n);
                double k = n < ProvisionalThreshold ? 40.0 : 20.0;
                current[pid] = EloCalculator.UpdateRating(current[pid], expected, score, k);
                played[pid] = n + 1;
            }

            foreach (var pid in home) UpdateOne(pid, homeRating, awayRating, expectedHome, scoreHome);
            foreach (var pid in away) UpdateOne(pid, awayRating, homeRating, expectedAway, scoreAway);
        }

        var result = new List<PendingSuggestion>();
        foreach (var pid in participantIds)
        {
            if (!played.TryGetValue(pid, out var n) || n == 0) continue;
            var pre = preRatings[pid];
            var post = current[pid];
            result.Add(new PendingSuggestion(
                PlayerId: pid,
                PreviousRating: pre,
                SuggestedRating: post,
                Delta: post - pre,
                RubbersPlayed: n));
        }
        return result;
    }

    /// <summary>
    /// Persists the suggestion for one player: writes a <c>SeasonEnd</c>
    /// snapshot on the previous competition and a <c>SeasonStart</c> snapshot
    /// on the current competition. Both are upserts, so calling again with
    /// new computed values overwrites the prior accepted state.
    /// </summary>
    public async Task<PendingSuggestion?> AcceptSuggestionAsync(
        int competitionId, int playerId, string? acceptedByUserId, CancellationToken ct = default)
    {
        var suggestions = await ComputePendingSuggestionsAsync(competitionId, ct);
        var s = suggestions.FirstOrDefault(x => x.PlayerId == playerId);
        if (s is null) return null;
        await PersistAcceptanceAsync(competitionId, s, acceptedByUserId, ct);
        return s;
    }

    public async Task<IReadOnlyList<PendingSuggestion>> AcceptAllSuggestionsAsync(
        int competitionId, string? acceptedByUserId, CancellationToken ct = default)
    {
        var suggestions = await ComputePendingSuggestionsAsync(competitionId, ct);
        foreach (var s in suggestions)
            await PersistAcceptanceAsync(competitionId, s, acceptedByUserId, ct);
        return suggestions;
    }

    private async Task PersistAcceptanceAsync(
        int competitionId, PendingSuggestion s, string? userId, CancellationToken ct)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var parentId = await db.Competition
            .Where(c => c.CompetitionID == competitionId)
            .Select(c => c.SeededFromCompetitionId)
            .FirstOrDefaultAsync(ct);
        if (parentId is null) return;

        bool provisional = s.RubbersPlayed < ProvisionalThreshold;
        await UpsertSnapshotAsync(db, competitionId: parentId.Value, playerId: s.PlayerId,
            kind: PlayerRatingSnapshotKind.SeasonEnd, rating: s.SuggestedRating,
            rubbersPlayed: s.RubbersPlayed, isProvisional: provisional, userId: userId, ct: ct);
        await UpsertSnapshotAsync(db, competitionId: competitionId, playerId: s.PlayerId,
            kind: PlayerRatingSnapshotKind.SeasonStart, rating: s.SuggestedRating,
            rubbersPlayed: 0, isProvisional: provisional, userId: userId, ct: ct);
        await db.SaveChangesAsync(ct);
    }

    private static async Task UpsertSnapshotAsync(
        MyDbContext db, int competitionId, int playerId, PlayerRatingSnapshotKind kind,
        double rating, int rubbersPlayed, bool isProvisional, string? userId, CancellationToken ct)
    {
        var existing = await db.PlayerRatingSnapshots
            .FirstOrDefaultAsync(s =>
                s.CompetitionId == competitionId
                && s.PlayerId == playerId
                && s.Kind == kind, ct);
        if (existing is null)
        {
            db.PlayerRatingSnapshots.Add(new Models.PlayerRatingSnapshot
            {
                CompetitionId = competitionId,
                PlayerId = playerId,
                Kind = kind,
                Rating = rating,
                RubbersPlayed = rubbersPlayed,
                IsProvisional = isProvisional,
                ComputedAt = DateTime.UtcNow,
                AcceptedByUserId = userId,
            });
        }
        else
        {
            existing.Rating = rating;
            existing.RubbersPlayed = rubbersPlayed;
            existing.IsProvisional = isProvisional;
            existing.ComputedAt = DateTime.UtcNow;
            existing.AcceptedByUserId = userId;
        }
    }

    public record DivisionPlacement(int PlayerId, int SuggestedDivisionId, string SuggestedDivisionName);
    public record RolePlacement(int PlayerId, string SuggestedRole);

    /// <summary>
    /// Sort participants by rating-desc and partition them across the
    /// competition's divisions (ranked 1 = top). Equal partitioning; remainder
    /// players go to the top divisions so the strongest tier is slightly
    /// larger when it doesn't divide evenly. Only returns entries where the
    /// suggested division differs from the participant's current division.
    /// Players without an explicit rating snapshot are treated as the default
    /// (1500), so a brand-new roster still gets a useful auto-assignment
    /// (admin overrides row-by-row).
    /// </summary>
    public async Task<IReadOnlyList<DivisionPlacement>> SuggestDivisionPlacementsAsync(
        int competitionId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var divisions = await db.CompetitionDivisions
            .AsNoTracking()
            .Where(d => d.CompetitionId == competitionId)
            .OrderBy(d => d.Rank)
            .ToListAsync(ct);
        if (divisions.Count == 0) return Array.Empty<DivisionPlacement>();

        var participants = await db.CompetitionParticipants
            .AsNoTracking()
            .Where(p => p.CompetitionId == competitionId)
            .Select(p => new { p.PlayerId, p.CompetitionDivisionId })
            .ToListAsync(ct);
        if (participants.Count == 0) return Array.Empty<DivisionPlacement>();

        var ratings = await GetRosterRatingsAsync(competitionId, ct);

        var ranked = participants
            .OrderByDescending(p => ratings.TryGetValue(p.PlayerId, out var r) ? r.Value : DefaultRating)
            .ThenBy(p => p.PlayerId)
            .ToList();

        int perDivision = ranked.Count / divisions.Count;
        int remainder = ranked.Count % divisions.Count;
        // Distribute the remainder into the top divisions so the strongest
        // tier is slightly larger if it doesn't divide evenly. Admin can
        // override row-by-row regardless.
        var result = new List<DivisionPlacement>();
        int cursor = 0;
        for (int i = 0; i < divisions.Count; i++)
        {
            int take = perDivision + (i < remainder ? 1 : 0);
            for (int j = 0; j < take && cursor < ranked.Count; j++, cursor++)
            {
                var p = ranked[cursor];
                if (p.CompetitionDivisionId == divisions[i].CompetitionDivisionId) continue;
                result.Add(new DivisionPlacement(p.PlayerId,
                    divisions[i].CompetitionDivisionId, divisions[i].Name));
            }
        }
        return result;
    }

    /// <summary>
    /// For each team, run the rating-aware MTT assigner over its members and
    /// surface any role that would differ from the participant's current role.
    /// TeamMatch competitions only — returns empty for other formats. Players
    /// without an explicit rating snapshot are treated as the default (1500)
    /// so a fresh roster gets a usable assignment (with all-equal ratings
    /// the assigner falls back to alphabetical, matching the legacy AssignMtt).
    /// </summary>
    public async Task<IReadOnlyList<RolePlacement>> SuggestRolePlacementsAsync(
        int competitionId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var comp = await db.Competition
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.CompetitionID == competitionId, ct);
        if (comp is null || comp.CompetitionFormat != CompetitionFormat.TeamMatch)
            return Array.Empty<RolePlacement>();

        var ratings = await GetRosterRatingsAsync(competitionId, ct);

        var participants = await db.CompetitionParticipants
            .AsNoTracking()
            .Include(p => p.Player)
            .Where(p => p.CompetitionId == competitionId && p.TeamId.HasValue)
            .ToListAsync(ct);
        if (participants.Count == 0) return Array.Empty<RolePlacement>();

        var assigner = RubberTemplateRegistry.GetRoleAssigner(RubberTemplateRegistry.MttRatedKey);
        if (assigner is null) return Array.Empty<RolePlacement>();

        var result = new List<RolePlacement>();
        foreach (var teamGroup in participants.GroupBy(p => p.TeamId!.Value))
        {
            var members = teamGroup.ToList();
            var candidates = members.Select(m => new RubberTemplateRegistry.AssignmentCandidate(
                m.PlayerId,
                m.Player?.DisplayName ?? "",
                m.Player?.Sex,
                ratings.TryGetValue(m.PlayerId, out var r) ? r.Value : DefaultRating)).ToList();
            var assignments = assigner(candidates);
            foreach (var m in members)
            {
                if (!assignments.TryGetValue(m.PlayerId, out var suggested)) continue;
                if (m.Role == suggested) continue;
                result.Add(new RolePlacement(m.PlayerId, suggested));
            }
        }
        return result;
    }

    /// <summary>
    /// Persist a division placement suggestion to the participant row. No
    /// validation here beyond row existence — the caller (admin service) gates
    /// edit permission.
    /// </summary>
    public async Task ApplyDivisionPlacementAsync(
        int competitionId, int playerId, int divisionId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var row = await db.CompetitionParticipants.FindAsync([competitionId, playerId], ct)
            ?? throw new KeyNotFoundException("Participant not found.");
        row.CompetitionDivisionId = divisionId;
        await db.SaveChangesAsync(ct);
    }

    public async Task ApplyRolePlacementAsync(
        int competitionId, int playerId, string role, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var row = await db.CompetitionParticipants.FindAsync([competitionId, playerId], ct)
            ?? throw new KeyNotFoundException("Participant not found.");
        row.Role = role;
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Upserts a <c>SeasonStart</c> snapshot on the current competition for the
    /// given player. Used to set the admin-controlled initial rating before any
    /// season has been played. Idempotent — re-calling with a new value just
    /// updates the existing row.
    /// </summary>
    public async Task SetInitialRatingAsync(
        int competitionId, int playerId, double rating, string? acceptedByUserId,
        CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        await UpsertSnapshotAsync(db, competitionId, playerId,
            kind: PlayerRatingSnapshotKind.SeasonStart, rating: rating,
            rubbersPlayed: 0, isProvisional: false, userId: acceptedByUserId, ct: ct);
        await db.SaveChangesAsync(ct);
    }
}
