using DropShot.Data;
using DropShot.Models;
using DropShot.Shared.Dtos;
using DropShot.UI.Services;
using DropShot.UI.Services.Auth;
using Microsoft.EntityFrameworkCore;

namespace DropShot.Services;

/// <summary>
/// Web implementation of <see cref="IMatchSetupService"/>. Mirrors the
/// inline EF queries <c>MatchSetupWizard.razor</c> ran before the Phase 7
/// move.
/// </summary>
public sealed class WebMatchSetupService(
    IDbContextFactory<MyDbContext> dbFactory,
    ICurrentUser currentUser) : IMatchSetupService
{
    public async Task<MatchSetupBootstrapDto> GetBootstrapAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var userId = currentUser.UserId;

        WizardSelfPlayerDto? me = null;
        IReadOnlyList<WizardPlayerDto> myLight = [];
        IReadOnlyList<WizardPlayerDto> myBookmarked = [];
        IReadOnlyList<WizardFuzzyItemDto> fuzzyIndex;

        if (!string.IsNullOrEmpty(userId))
        {
            var meRow = await db.Players
                .AsNoTracking()
                .Include(p => p.User)
                .Where(p => p.UserId == userId)
                .Select(p => new
                {
                    p.PlayerId,
                    p.DisplayName,
                    LinkedAvatar = p.User != null ? p.User.ProfileImagePath : null,
                    OwnAvatar = p.ProfileImagePath
                })
                .FirstOrDefaultAsync(ct);
            if (meRow is not null)
            {
                me = new WizardSelfPlayerDto(
                    meRow.PlayerId,
                    meRow.DisplayName,
                    meRow.LinkedAvatar ?? meRow.OwnAvatar);
            }

            myLight = await db.Players
                .AsNoTracking()
                .Where(p => p.IsLight && p.CreatedByUserId == userId)
                .OrderBy(p => p.DisplayName)
                .Select(p => new WizardPlayerDto(p.PlayerId, p.DisplayName, true, p.ProfileImagePath))
                .ToListAsync(ct);

            var bookmarkedIds = await db.UserPlayers
                .Where(up => up.UserId == userId)
                .Select(up => up.PlayerId)
                .ToListAsync(ct);

            if (bookmarkedIds.Count > 0)
            {
                myBookmarked = await db.Players
                    .AsNoTracking()
                    .Include(p => p.User)
                    .Where(p => bookmarkedIds.Contains(p.PlayerId) && p.UserId != userId)
                    .OrderBy(p => p.DisplayName)
                    .Select(p => new WizardPlayerDto(
                        p.PlayerId,
                        p.DisplayName,
                        p.IsLight,
                        p.User != null ? p.User.ProfileImagePath : p.ProfileImagePath))
                    .ToListAsync(ct);
            }

            // Fuzzy index: my light + my bookmarked + every other full player
            // (so the "Did you mean?" nudge can suggest verified replacements
            // even if they're not bookmarked). Excludes self from "all full".
            var lightItems = myLight
                .Select(p => new WizardFuzzyItemDto(p.PlayerId, p.DisplayName, null, null, true))
                .ToList();
            var verifiedItems = myBookmarked
                .Where(p => !p.IsLight)
                .Select(p => new WizardFuzzyItemDto(p.PlayerId, p.DisplayName, null, null, false))
                .ToList();
            var myIds = lightItems.Select(i => i.PlayerId)
                .Concat(verifiedItems.Select(i => i.PlayerId))
                .ToHashSet();

            var additionalFull = await db.Players
                .AsNoTracking()
                .Where(p => !p.IsLight && p.UserId != userId)
                .OrderBy(p => p.DisplayName)
                .Select(p => new { p.PlayerId, p.DisplayName, p.FirstName, p.LastName })
                .ToListAsync(ct);

            fuzzyIndex = lightItems
                .Concat(verifiedItems)
                .Concat(additionalFull
                    .Where(p => !myIds.Contains(p.PlayerId))
                    .Select(p => new WizardFuzzyItemDto(p.PlayerId, p.DisplayName, p.FirstName, p.LastName, false)))
                .ToList();
        }
        else
        {
            // Anonymous: still seed the fuzzy index with all full players so
            // the wizard's autocomplete and "Did you mean?" nudge work.
            fuzzyIndex = await db.Players
                .AsNoTracking()
                .Where(p => !p.IsLight)
                .OrderBy(p => p.DisplayName)
                .Select(p => new WizardFuzzyItemDto(p.PlayerId, p.DisplayName, p.FirstName, p.LastName, false))
                .ToListAsync(ct);
        }

        var clubs = await db.Clubs
            .AsNoTracking()
            .OrderBy(c => c.Name)
            .Select(c => new WizardClubDto(c.ClubId, c.Name))
            .ToListAsync(ct);

        return new MatchSetupBootstrapDto(me, myLight, myBookmarked, clubs, fuzzyIndex);
    }

    public async Task<List<WizardCourtDto>> GetCourtsByClubAsync(int clubId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.Courts
            .AsNoTracking()
            .Where(c => c.ClubId == clubId)
            .OrderBy(c => c.Name)
            .Select(c => new WizardCourtDto(c.CourtId, c.Name))
            .ToListAsync(ct);
    }

    public async Task AutoBookmarkPlayersAsync(
        AutoBookmarkPlayersRequest request, CancellationToken ct = default)
    {
        var userId = currentUser.UserId;
        if (string.IsNullOrEmpty(userId)) return;
        if (request.PlayerIds is null || request.PlayerIds.Count == 0) return;

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var existing = await db.UserPlayers
            .Where(up => up.UserId == userId && request.PlayerIds.Contains(up.PlayerId))
            .Select(up => up.PlayerId)
            .ToListAsync(ct);
        var existingSet = existing.ToHashSet();

        foreach (var pid in request.PlayerIds.Distinct())
        {
            if (existingSet.Contains(pid)) continue;
            db.UserPlayers.Add(new UserPlayer { UserId = userId, PlayerId = pid });
        }
        await db.SaveChangesAsync(ct);
    }
}
