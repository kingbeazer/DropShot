using DropShot.Data;
using DropShot.Models;
using DropShot.Shared;
using Microsoft.EntityFrameworkCore;

namespace DropShot.Services;

/// <summary>
/// Daily sweep that decays inactive SinglesLadder participants' Elo ratings
/// and sends warning emails when a player is about to enter the decay window.
/// Idempotent: re-running the sweep within the same week is a no-op per player.
///
/// Designed for testability — pass <paramref name="now"/> explicitly so unit
/// tests can simulate days passing without touching wall-clock time.
/// </summary>
public static class LadderInactivityService
{
    /// <summary>How long after a player's last activity before decay begins.</summary>
    public const int GraceDays = 21;

    /// <summary>Rating points removed per fully-elapsed week of post-grace inactivity.</summary>
    public const double DecayPointsPerWeek = 10.0;

    /// <summary>Send a warning email this many days before decay starts.</summary>
    public const int WarnDaysBefore = 3;

    public sealed record DecayRunResult(int DecayEventsApplied, int WarningsSent);

    public static async Task<DecayRunResult> RunSweepAsync(
        MyDbContext db,
        DateTime now,
        AdminEmailService? email,
        CancellationToken ct = default)
    {
        // Pull every active SinglesLadder FullPlayer participant in one query.
        // Small enough for clubs of any realistic size; ladders rarely exceed
        // a few hundred participants.
        var rows = await db.CompetitionParticipants
            .Where(p => p.Status == ParticipantStatus.FullPlayer
                        && p.Competition.CompetitionFormat == CompetitionFormat.SinglesLadder
                        && p.Competition.IsStarted
                        && !p.Competition.IsArchived)
            .Include(p => p.Competition)
            .Include(p => p.Player)
            .ToListAsync(ct);

        int decayCount = 0;
        int warnCount = 0;

        foreach (var p in rows)
        {
            // Activity anchor is the most recent of last match / registration —
            // i.e. when the player was last "active" in the ladder. LastDecay
            // is gated separately so weekly steps continue accruing while the
            // player stays idle.
            DateTime anchor = p.LastMatchAt ?? p.RegisteredAt;
            int daysIdle = Math.Max(0, (int)Math.Floor((now - anchor).TotalDays));
            int graceRemaining = GraceDays - daysIdle;

            // ── Warning pass ────────────────────────────────────────────────
            // Fires when we're inside [1, WarnDaysBefore] days from decay
            // starting, and we haven't already warned about the current idle
            // stretch (player hasn't played since the last warning, and the
            // warning isn't stale beyond a full grace cycle).
            if (graceRemaining > 0 && graceRemaining <= WarnDaysBefore)
            {
                bool alreadyWarned = p.LastInactivityWarningAt is DateTime w
                    && (p.LastMatchAt is null || p.LastMatchAt <= w)
                    && (now - w).TotalDays < GraceDays;

                if (!alreadyWarned)
                {
                    if (email is not null && !string.IsNullOrEmpty(p.Player.Email))
                    {
                        await email.SendLadderInactivityWarningAsync(p.Player, p.Competition, graceRemaining, ct);
                    }
                    p.LastInactivityWarningAt = now;
                    warnCount++;
                }
            }

            // ── Decay pass ──────────────────────────────────────────────────
            // Past grace, and either no prior decay or the prior decay is at
            // least a week old. One step per sweep — re-running today is a
            // no-op for the same player.
            bool pastGrace = daysIdle >= GraceDays;
            bool weekSinceLastDecay = p.LastDecayAppliedAt is not DateTime ld
                                       || (now - ld).TotalDays >= 7;

            if (pastGrace && weekSinceLastDecay)
            {
                double floor = p.Competition.LadderStartingRating;
                double currentRating = p.EloRating;
                double delta = Math.Min(DecayPointsPerWeek, Math.Max(0, currentRating - floor));

                // Always advance the anchor so the next-step gate ticks forward,
                // even when there's no room left to decay further.
                p.LastDecayAppliedAt = now;

                if (delta > 0)
                {
                    p.EloRating = currentRating - delta;
                    db.LadderInactivityDecays.Add(new LadderInactivityDecay
                    {
                        CompetitionId = p.CompetitionId,
                        PlayerId = p.PlayerId,
                        AppliedAt = now,
                        RatingBefore = currentRating,
                        RatingAfter = p.EloRating,
                        DaysInactive = daysIdle,
                    });
                    decayCount++;
                }
            }
        }

        await db.SaveChangesAsync(ct);
        return new DecayRunResult(decayCount, warnCount);
    }
}
