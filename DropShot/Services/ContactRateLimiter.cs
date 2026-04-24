using Microsoft.Extensions.Caching.Memory;

namespace DropShot.Services;

/// <summary>
/// Lightweight in-memory sliding-window limiter for the public /contact form.
/// Three submissions per 15 minutes per key (either IP or email) is enough to
/// stop casual abuse without punishing legitimate resubmits.
/// </summary>
public class ContactRateLimiter(IMemoryCache cache)
{
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(15);
    private const int MaxPerWindow = 3;

    /// <summary>
    /// Records an attempt for the given key. Returns true if the attempt is
    /// within the allowed window, false if the caller has exceeded the limit.
    /// </summary>
    public bool TryRecord(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return true;

        var cacheKey = $"contact-rl:{key.ToLowerInvariant()}";
        var stamps = cache.Get<List<DateTime>>(cacheKey) ?? [];
        var cutoff = DateTime.UtcNow - Window;
        stamps = stamps.Where(t => t >= cutoff).ToList();

        if (stamps.Count >= MaxPerWindow) return false;

        stamps.Add(DateTime.UtcNow);
        cache.Set(cacheKey, stamps, Window);
        return true;
    }
}
