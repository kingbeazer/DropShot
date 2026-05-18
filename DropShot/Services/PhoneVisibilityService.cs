using DropShot.Data;
using DropShot.Models;
using DropShot.Shared;
using Microsoft.EntityFrameworkCore;

namespace DropShot.Services;

/// <summary>
/// Single source of truth for whether one player's mobile number is allowed
/// to be shown to another in the context of a competition. Encapsulates the
/// per-competition consent record (CompetitionEntryConsent), the under-13
/// hard block, and the same-division peer scope. Admin bypass is delegated
/// to the caller via the <c>viewerIsCompetitionAdmin</c> flag — admins
/// retain visibility under a legitimate-interest basis, distinct from the
/// peer-consent flow this service governs.
/// </summary>
// TODO(privacy): In-app messaging that brokers contact without revealing
// numbers is the longer-term goal. Once that lands, CanViewPhoneNumber should
// default to false for peers and we'd surface a "message via app" action
// instead of returning the number.
public interface IPhoneVisibilityService
{
    /// <summary>
    /// True iff the viewer is allowed to see <paramref name="targetPlayerId"/>'s
    /// mobile number in the context of <paramref name="competitionId"/>.
    /// </summary>
    Task<bool> CanViewPhoneNumberAsync(
        string viewerUserId,
        int targetPlayerId,
        int competitionId,
        bool viewerIsCompetitionAdmin,
        CancellationToken ct = default);

    /// <summary>
    /// Bulk variant for list rendering. Returns the subset of
    /// <paramref name="candidatePlayerIds"/> whose mobile numbers the viewer
    /// is allowed to see.
    /// </summary>
    Task<HashSet<int>> VisiblePhoneNumberPlayerIdsAsync(
        string viewerUserId,
        int competitionId,
        IReadOnlyCollection<int> candidatePlayerIds,
        bool viewerIsCompetitionAdmin,
        CancellationToken ct = default);
}

public sealed class PhoneVisibilityService(
    IDbContextFactory<MyDbContext> dbFactory) : IPhoneVisibilityService
{
    /// <summary>
    /// Bumped whenever the consent dialog template wording changes. The
    /// consent record stores both the version and the rendered text (the
    /// version makes bulk reporting easy; the rendered text is the legal
    /// audit trail). Server rejects entry requests whose Version doesn't
    /// match — that forces a stale client to reload before continuing.
    /// </summary>
    public const string CurrentConsentVersion = "v1-2026-05";

    /// <summary>
    /// Consent version stamped on rows the admin attests to on behalf of
    /// the player (the email-asked-to-enter path). Distinct from
    /// <see cref="CurrentConsentVersion"/> so audits can filter
    /// admin-attested vs self-asserted consents — the former are materially
    /// weaker (no direct data-subject click) and rely on the
    /// <c>ConsentWordingShown</c> field capturing the admin's evidence.
    /// </summary>
    public const string AdminRecordedConsentVersion = "v1-2026-05-admin";

    /// <summary>
    /// Hard age limit, in years. Players under this age never have their
    /// mobile number shared with peers, regardless of consent. Matches the
    /// "Children" clause in Privacy.razor §9 and DropShot's stance of not
    /// being directed at under-13s.
    /// </summary>
    public const int MinimumAgeForPhoneSharing = 13;

    public async Task<bool> CanViewPhoneNumberAsync(
        string viewerUserId,
        int targetPlayerId,
        int competitionId,
        bool viewerIsCompetitionAdmin,
        CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(viewerUserId)) return false;

        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var target = await db.Players
            .AsNoTracking()
            .Where(p => p.PlayerId == targetPlayerId)
            .Select(p => new { p.PlayerId, p.UserId, p.DateOfBirth })
            .FirstOrDefaultAsync(ct);
        if (target is null) return false;

        // 1. Self — always visible.
        if (!string.IsNullOrEmpty(target.UserId) && target.UserId == viewerUserId)
            return true;

        // 2. Under-13 block. A null DateOfBirth is "unknown" rather than
        //    "minor": the block requires a positive signal. Future parental-
        //    consent flow will plug in here.
        if (target.DateOfBirth is { } dob)
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            if (EligibilityEvaluator.AgeOn(today, dob) < MinimumAgeForPhoneSharing)
                return false;
        }

        // 3. Admin bypass — distinct from peer consent. See class comment.
        if (viewerIsCompetitionAdmin) return true;

        // 4. Both must be active (non-withdrawn, non-disqualified) participants
        //    in the same competition.
        var participants = await db.CompetitionParticipants
            .AsNoTracking()
            .Where(cp => cp.CompetitionId == competitionId
                         && (cp.Player!.UserId == viewerUserId || cp.PlayerId == targetPlayerId))
            .Select(cp => new
            {
                cp.PlayerId,
                cp.Status,
                cp.CompetitionDivisionId,
                ViewerMatch = cp.Player!.UserId == viewerUserId
            })
            .ToListAsync(ct);

        var viewerRow = participants.FirstOrDefault(p => p.ViewerMatch);
        var targetRow = participants.FirstOrDefault(p => p.PlayerId == targetPlayerId);
        if (viewerRow is null || targetRow is null) return false;
        if (!IsActiveParticipant(viewerRow.Status) || !IsActiveParticipant(targetRow.Status))
            return false;

        // 5. Same-division check (mirrors CanSeeContactForDivision in
        //    ViewCompetition.razor). When the competition has no divisions
        //    both rows have a null CompetitionDivisionId and they match.
        if (viewerRow.CompetitionDivisionId != targetRow.CompetitionDivisionId)
            return false;

        // 6. Most-recent CompetitionEntryConsent row for the target must be
        //    active (WithdrawnUtc IS NULL).
        var latestConsent = await db.CompetitionEntryConsents
            .AsNoTracking()
            .Where(c => c.CompetitionId == competitionId && c.PlayerId == targetPlayerId)
            .OrderByDescending(c => c.ConsentGivenUtc)
            .Select(c => new { c.WithdrawnUtc })
            .FirstOrDefaultAsync(ct);
        return latestConsent is { WithdrawnUtc: null };
    }

    public async Task<HashSet<int>> VisiblePhoneNumberPlayerIdsAsync(
        string viewerUserId,
        int competitionId,
        IReadOnlyCollection<int> candidatePlayerIds,
        bool viewerIsCompetitionAdmin,
        CancellationToken ct = default)
    {
        if (candidatePlayerIds.Count == 0 || string.IsNullOrEmpty(viewerUserId))
            return [];

        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var candidateIds = candidatePlayerIds.ToHashSet();

        var viewerPlayer = await db.Players
            .AsNoTracking()
            .Where(p => p.UserId == viewerUserId)
            .Select(p => new { p.PlayerId })
            .FirstOrDefaultAsync(ct);

        var candidates = await db.Players
            .AsNoTracking()
            .Where(p => candidateIds.Contains(p.PlayerId))
            .Select(p => new { p.PlayerId, p.UserId, p.DateOfBirth })
            .ToListAsync(ct);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var visible = new HashSet<int>();

        // Self is always visible — and rules 1+2 apply even when the viewer
        // isn't a participant of this competition (their own profile page).
        foreach (var c in candidates)
        {
            if (!string.IsNullOrEmpty(c.UserId) && c.UserId == viewerUserId)
                visible.Add(c.PlayerId);
        }

        // Under-13s drop out unconditionally for the remainder.
        var eligibleByAge = candidates
            .Where(c => c.DateOfBirth is null
                        || EligibilityEvaluator.AgeOn(today, c.DateOfBirth.Value) >= MinimumAgeForPhoneSharing)
            .Select(c => c.PlayerId)
            .ToHashSet();

        if (viewerIsCompetitionAdmin)
        {
            foreach (var id in eligibleByAge) visible.Add(id);
            return visible;
        }

        if (viewerPlayer is null) return visible;

        var participantRows = await db.CompetitionParticipants
            .AsNoTracking()
            .Where(cp => cp.CompetitionId == competitionId
                         && (cp.PlayerId == viewerPlayer.PlayerId || candidateIds.Contains(cp.PlayerId)))
            .Select(cp => new { cp.PlayerId, cp.Status, cp.CompetitionDivisionId })
            .ToListAsync(ct);

        var viewerRow = participantRows.FirstOrDefault(p => p.PlayerId == viewerPlayer.PlayerId);
        if (viewerRow is null || !IsActiveParticipant(viewerRow.Status))
            return visible;

        var sameDivisionPeerIds = participantRows
            .Where(p => p.PlayerId != viewerPlayer.PlayerId
                        && IsActiveParticipant(p.Status)
                        && p.CompetitionDivisionId == viewerRow.CompetitionDivisionId
                        && eligibleByAge.Contains(p.PlayerId))
            .Select(p => p.PlayerId)
            .ToHashSet();

        if (sameDivisionPeerIds.Count == 0) return visible;

        // For each same-division peer, take the latest consent row.
        var latestConsents = await db.CompetitionEntryConsents
            .AsNoTracking()
            .Where(c => c.CompetitionId == competitionId && sameDivisionPeerIds.Contains(c.PlayerId))
            .GroupBy(c => c.PlayerId)
            .Select(g => new
            {
                PlayerId = g.Key,
                Latest = g.OrderByDescending(c => c.ConsentGivenUtc)
                    .Select(c => new { c.WithdrawnUtc })
                    .First()
            })
            .ToListAsync(ct);

        foreach (var row in latestConsents)
        {
            if (row.Latest.WithdrawnUtc is null)
                visible.Add(row.PlayerId);
        }

        return visible;
    }

    private static bool IsActiveParticipant(ParticipantStatus status) =>
        status != ParticipantStatus.Withdrawn && status != ParticipantStatus.Disqualified;
}
