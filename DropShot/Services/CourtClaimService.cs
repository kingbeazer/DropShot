using DropShot.Data;
using DropShot.Models;
using Microsoft.EntityFrameworkCore;

namespace DropShot.Services;

/// <summary>
/// Resolves who currently owns a court when a new scorer tries to
/// start a match. Matches with no score activity for longer than
/// <see cref="AbandonAfter"/> are treated as abandoned and auto-closed
/// so the next user can claim the court without interaction.
/// </summary>
public sealed class CourtClaimService
{
    public static readonly TimeSpan AbandonAfter = TimeSpan.FromMinutes(10);
    public static readonly TimeSpan GraceAfterYes = TimeSpan.FromMinutes(15);

    private readonly IDbContextFactory<MyDbContext> _dbFactory;

    public CourtClaimService(IDbContextFactory<MyDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task<CourtClaimResult> EvaluateAsync(int courtId, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var active = await db.SavedMatch
            .Where(m => m.CourtId == courtId && !m.Complete)
            .OrderByDescending(m => m.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (active is null)
            return CourtClaimResult.Free();

        var now = DateTime.UtcNow;
        // EF Core strips DateTimeKind on round-trip, so columns read back as
        // Kind=Unspecified. Pin them to UTC so callers can safely do local
        // conversions ("until HH:mm") without getting a past time.
        var lastSeen = AsUtc(active.LastActivityAt ?? active.CreatedAt);
        var graceUntil = active.ClaimGraceUntilUtc.HasValue
            ? (DateTime?)AsUtc(active.ClaimGraceUntilUtc.Value)
            : null;

        if (now - lastSeen >= AbandonAfter)
        {
            active.Complete = true;
            active.CompletedAt = now;
            active.ClaimGraceUntilUtc = null;
            await db.SaveChangesAsync(ct);
            return CourtClaimResult.StaleAutoClosed(active.SavedMatchId);
        }

        if (graceUntil is { } until && until > now)
            return CourtClaimResult.InGrace(active.SavedMatchId, until,
                BuildOccupantLabel(active));

        return CourtClaimResult.NeedsChallenge(active.SavedMatchId,
            BuildOccupantLabel(active));
    }

    public async Task<bool> IsStaleAsync(int savedMatchId, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var match = await db.SavedMatch.FirstOrDefaultAsync(m => m.SavedMatchId == savedMatchId, ct);
        if (match is null || match.Complete) return true;
        var lastSeen = AsUtc(match.LastActivityAt ?? match.CreatedAt);
        return DateTime.UtcNow - lastSeen >= AbandonAfter;
    }

    /// <summary>
    /// Finds the most recent incomplete SavedMatch owned by this user so the
    /// "Play" buttons can offer to resume or end it before starting a new one.
    /// </summary>
    public async Task<SavedMatch?> GetUserActiveMatchAsync(
        string userId, int? excludingSavedMatchId = null, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(userId)) return null;
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var q = db.SavedMatch.Where(m => m.UserId == userId && !m.Complete);
        if (excludingSavedMatchId.HasValue)
            q = q.Where(m => m.SavedMatchId != excludingSavedMatchId.Value);
        return await q.OrderByDescending(m => m.CreatedAt).FirstOrDefaultAsync(ct);
    }

    private static DateTime AsUtc(DateTime value) =>
        value.Kind == DateTimeKind.Utc ? value : DateTime.SpecifyKind(value, DateTimeKind.Utc);

    public async Task ExtendGraceAsync(int savedMatchId, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var match = await db.SavedMatch.FirstOrDefaultAsync(m => m.SavedMatchId == savedMatchId, ct);
        if (match is null || match.Complete) return;
        match.ClaimGraceUntilUtc = DateTime.UtcNow.Add(GraceAfterYes);
        match.LastActivityAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task EndMatchAsync(int savedMatchId, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var match = await db.SavedMatch.FirstOrDefaultAsync(m => m.SavedMatchId == savedMatchId, ct);
        if (match is null || match.Complete) return;
        match.Complete = true;
        match.CompletedAt = DateTime.UtcNow;
        match.ClaimGraceUntilUtc = null;
        await db.SaveChangesAsync(ct);
    }

    private static string BuildOccupantLabel(SavedMatch m)
    {
        var a = !string.IsNullOrWhiteSpace(m.Player3)
            ? $"{m.Player1} & {m.Player2}"
            : m.Player1 ?? "";
        var b = !string.IsNullOrWhiteSpace(m.Player3)
            ? $"{m.Player3} & {m.Player4}"
            : m.Player2 ?? "";
        if (string.IsNullOrWhiteSpace(a) && string.IsNullOrWhiteSpace(b))
            return "another match";
        if (string.IsNullOrWhiteSpace(b)) return a;
        return $"{a} vs {b}";
    }
}

public enum CourtClaimOutcome
{
    Free,
    StaleAutoClosed,
    InGrace,
    NeedsChallenge
}

public sealed record CourtClaimResult(
    CourtClaimOutcome Outcome,
    int? SavedMatchId = null,
    DateTime? GraceUntilUtc = null,
    string? OccupantLabel = null)
{
    public static CourtClaimResult Free() => new(CourtClaimOutcome.Free);
    public static CourtClaimResult StaleAutoClosed(int savedMatchId) =>
        new(CourtClaimOutcome.StaleAutoClosed, savedMatchId);
    public static CourtClaimResult InGrace(int savedMatchId, DateTime until, string label) =>
        new(CourtClaimOutcome.InGrace, savedMatchId, until, label);
    public static CourtClaimResult NeedsChallenge(int savedMatchId, string label) =>
        new(CourtClaimOutcome.NeedsChallenge, savedMatchId, null, label);
}
