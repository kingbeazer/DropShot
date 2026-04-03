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
        int[]? fallbackTimeSlots = null)
    {
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

    private static DateTime FallbackSlot(DateTime start, DateTime end, Random rng, int[] timeSlots)
    {
        var days = Math.Max(0, (end.Date - start.Date).Days);
        return start.Date
            .AddDays(days > 0 ? rng.Next(0, days + 1) : 0)
            .AddMinutes(timeSlots[rng.Next(timeSlots.Length)]);
    }
}
