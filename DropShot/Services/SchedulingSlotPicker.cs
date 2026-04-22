using DropShot.Models;

namespace DropShot.Services;

public static class SchedulingSlotPicker
{
    private static readonly int[] DefaultTimeSlots =
        [9 * 60, 10 * 60 + 30, 12 * 60, 13 * 60 + 30, 15 * 60, 16 * 60 + 30, 18 * 60];

    /// <summary>
    /// Picks a random valid DateTime within [windowStart, windowEnd].
    /// When match windows are defined, only days and times inside those windows are used.
    /// Falls back to default slots when no windows are defined or none fit.
    /// Custom fallback time slots (in minutes from midnight) can be provided to
    /// override the built-in defaults for courts with different operating hours.
    /// </summary>
    public static DateTime PickSlot(
        IReadOnlyList<CompetitionMatchWindow> windows,
        DateTime windowStart,
        DateTime windowEnd,
        Random rng,
        int[]? fallbackTimeSlots = null,
        int? divisionId = null)
    {
        windows = FilterForDivision(windows, divisionId);

        if (windows.Count > 0)
        {
            var candidates = new List<(DateTime date, TimeSpan time)>();
            for (var d = windowStart.Date; d <= windowEnd.Date; d = d.AddDays(1))
            {
                foreach (var w in windows.Where(w => w.DayOfWeek == d.DayOfWeek))
                {
                    for (var t = w.StartTime; t < w.EndTime; t = t.Add(TimeSpan.FromMinutes(30)))
                        candidates.Add((d, t));
                }
            }

            if (candidates.Count > 0)
            {
                var pick = candidates[rng.Next(candidates.Count)];
                return pick.date + pick.time;
            }
        }

        return FallbackSlot(windowStart, windowEnd, rng, fallbackTimeSlots ?? DefaultTimeSlots);
    }

    /// <summary>
    /// Picks a random valid (DateTime, CourtId) pair that does not collide with
    /// already-occupied slots. Two fixtures may share the same time only when
    /// they are on different courts.
    /// Falls back to <see cref="PickSlot"/> with no court when no courts are
    /// provided or all court-specific candidates are exhausted.
    /// </summary>
    public static (DateTime slot, int? courtId) PickCourtSlot(
        IReadOnlyList<CompetitionMatchWindow> windows,
        IReadOnlyList<Court> courts,
        HashSet<(DateTime, int?)> occupied,
        DateTime windowStart,
        DateTime windowEnd,
        Random rng,
        int? divisionId = null)
    {
        windows = FilterForDivision(windows, divisionId);

        if (courts.Count == 0)
            return (PickSlot(windows, windowStart, windowEnd, rng), null);

        var candidates = new List<(DateTime time, int courtId)>();

        for (var d = windowStart.Date; d <= windowEnd.Date; d = d.AddDays(1))
        {
            foreach (var court in courts)
            {
                // Court-specific windows for this court and day
                var courtWindows = windows
                    .Where(w => w.DayOfWeek == d.DayOfWeek && w.CourtId == court.CourtId)
                    .ToList();

                // If no court-specific windows, use global windows (CourtId == null)
                if (courtWindows.Count == 0)
                {
                    courtWindows = windows
                        .Where(w => w.DayOfWeek == d.DayOfWeek && w.CourtId == null)
                        .ToList();
                }

                foreach (var w in courtWindows)
                {
                    for (var t = w.StartTime; t < w.EndTime; t = t.Add(TimeSpan.FromMinutes(30)))
                        candidates.Add((d + t, court.CourtId));
                }
            }
        }

        // Fall back to default slots per court when no windows defined
        if (candidates.Count == 0 && windows.Count == 0)
        {
            foreach (var court in courts)
            {
                for (var d = windowStart.Date; d <= windowEnd.Date; d = d.AddDays(1))
                {
                    foreach (var mins in DefaultTimeSlots)
                        candidates.Add((d.AddMinutes(mins), court.CourtId));
                }
            }
        }

        // Remove occupied slots
        var available = candidates.Where(c => !occupied.Contains((c.time, c.courtId))).ToList();

        if (available.Count > 0)
        {
            var pick = available[rng.Next(available.Count)];
            return (pick.time, pick.courtId);
        }

        // All court slots exhausted — fall back to time-only
        return (PickSlot(windows, windowStart, windowEnd, rng), null);
    }

    /// <summary>
    /// Narrows a window list to those applicable to the given division: a
    /// shared (DivisionId == null) window applies to every division, and a
    /// division-tagged window only applies to its own division.
    /// </summary>
    private static IReadOnlyList<CompetitionMatchWindow> FilterForDivision(
        IReadOnlyList<CompetitionMatchWindow> windows, int? divisionId)
    {
        if (windows.Count == 0) return windows;
        if (divisionId is null)
        {
            // No division context → only shared windows are eligible. Callers
            // that pass a division ID of null because the competition is
            // non-divisional still get all their (null-divisioned) windows.
            if (windows.All(w => w.CompetitionDivisionId == null)) return windows;
            return windows.Where(w => w.CompetitionDivisionId == null).ToList();
        }
        return windows
            .Where(w => w.CompetitionDivisionId == null || w.CompetitionDivisionId == divisionId)
            .ToList();
    }

    private static DateTime FallbackSlot(DateTime start, DateTime end, Random rng, int[] timeSlots)
    {
        var days = Math.Max(0, (end.Date - start.Date).Days);
        return start.Date
            .AddDays(days > 0 ? rng.Next(0, days + 1) : 0)
            .AddMinutes(timeSlots[rng.Next(timeSlots.Length)]);
    }
}
