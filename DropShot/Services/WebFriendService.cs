using DropShot.Data;
using DropShot.Models;
using DropShot.Shared;
using DropShot.Shared.Dtos;
using DropShot.UI.Services;
using DropShot.UI.Services.Auth;
using Microsoft.EntityFrameworkCore;

namespace DropShot.Services;

public sealed class WebFriendService(
    IDbContextFactory<MyDbContext> dbFactory,
    ICurrentUser currentUser) : IFriendService
{
    private async Task<int?> GetMyPlayerIdAsync(MyDbContext db, CancellationToken ct)
    {
        var userId = currentUser.UserId;
        if (string.IsNullOrEmpty(userId)) return null;
        return await db.Players
            .Where(p => p.UserId == userId)
            .Select(p => (int?)p.PlayerId)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<List<FriendDto>> GetFriendsAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var myPlayerId = await GetMyPlayerIdAsync(db, ct);
        if (myPlayerId is null) return [];

        var rows = await db.PlayerFriends
            .Where(pf => pf.Status == FriendStatus.Accepted
                && (pf.PlayerId == myPlayerId || pf.FriendPlayerId == myPlayerId))
            .Include(pf => pf.Player)
            .Include(pf => pf.Friend)
            .ToListAsync(ct);

        return rows.Select(pf =>
        {
            var other = pf.PlayerId == myPlayerId ? pf.Friend : pf.Player;
            return new FriendDto(other.PlayerId, other.DisplayName, other.ProfileImagePath, other.UserId);
        }).ToList();
    }

    public async Task<List<FriendRequestDto>> GetIncomingRequestsAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var myPlayerId = await GetMyPlayerIdAsync(db, ct);
        if (myPlayerId is null) return [];

        return await db.PlayerFriends
            .Where(pf => pf.Status == FriendStatus.Pending && pf.FriendPlayerId == myPlayerId)
            .OrderByDescending(pf => pf.RequestedAt)
            .Select(pf => new FriendRequestDto(pf.Player.PlayerId, pf.Player.DisplayName, pf.Player.ProfileImagePath, pf.RequestedAt))
            .ToListAsync(ct);
    }

    public async Task<List<FriendRequestDto>> GetOutgoingRequestsAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var myPlayerId = await GetMyPlayerIdAsync(db, ct);
        if (myPlayerId is null) return [];

        return await db.PlayerFriends
            .Where(pf => pf.Status == FriendStatus.Pending && pf.PlayerId == myPlayerId)
            .OrderByDescending(pf => pf.RequestedAt)
            .Select(pf => new FriendRequestDto(pf.Friend.PlayerId, pf.Friend.DisplayName, pf.Friend.ProfileImagePath, pf.RequestedAt))
            .ToListAsync(ct);
    }

    public async Task<List<FriendSearchResultDto>> SearchPlayersAsync(string term, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(term)) return [];

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var myPlayerId = await GetMyPlayerIdAsync(db, ct);

        var matches = await db.Players
            .Where(p => !p.IsLight && p.UserId != null
                && p.PlayerId != myPlayerId
                && p.DisplayName.Contains(term))
            .OrderBy(p => p.DisplayName)
            .Take(20)
            .ToListAsync(ct);
        if (matches.Count == 0) return [];

        var matchIds = matches.Select(p => p.PlayerId).ToList();
        var relations = myPlayerId is null
            ? new List<PlayerFriend>()
            : await db.PlayerFriends
                .Where(pf => (pf.PlayerId == myPlayerId && matchIds.Contains(pf.FriendPlayerId))
                          || (pf.FriendPlayerId == myPlayerId && matchIds.Contains(pf.PlayerId)))
                .ToListAsync(ct);

        return matches.Select(p =>
        {
            var rel = relations.FirstOrDefault(pf => pf.PlayerId == p.PlayerId || pf.FriendPlayerId == p.PlayerId);
            return new FriendSearchResultDto(
                p.PlayerId, p.DisplayName, p.ProfileImagePath, p.UserId,
                IsFriend: rel?.Status == FriendStatus.Accepted,
                RequestPending: rel?.Status == FriendStatus.Pending);
        }).ToList();
    }

    public async Task SendFriendRequestAsync(int targetPlayerId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var myPlayerId = await GetMyPlayerIdAsync(db, ct);
        if (myPlayerId is null) return;
        if (myPlayerId.Value == targetPlayerId) return;

        var exists = await db.PlayerFriends.AnyAsync(pf =>
            (pf.PlayerId == myPlayerId.Value && pf.FriendPlayerId == targetPlayerId)
            || (pf.PlayerId == targetPlayerId && pf.FriendPlayerId == myPlayerId.Value), ct);
        if (exists) return;

        db.PlayerFriends.Add(new PlayerFriend
        {
            PlayerId = myPlayerId.Value,
            FriendPlayerId = targetPlayerId,
            Status = FriendStatus.Pending,
            RequestedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync(ct);
    }

    public async Task AcceptFriendRequestAsync(int requestingPlayerId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var myPlayerId = await GetMyPlayerIdAsync(db, ct);
        if (myPlayerId is null) return;

        var request = await db.PlayerFriends.FirstOrDefaultAsync(pf =>
            pf.PlayerId == requestingPlayerId && pf.FriendPlayerId == myPlayerId && pf.Status == FriendStatus.Pending, ct);
        if (request is null) return;

        request.Status = FriendStatus.Accepted;
        await db.SaveChangesAsync(ct);
    }

    public async Task RejectFriendRequestAsync(int requestingPlayerId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var myPlayerId = await GetMyPlayerIdAsync(db, ct);
        if (myPlayerId is null) return;

        var request = await db.PlayerFriends.FirstOrDefaultAsync(pf =>
            pf.PlayerId == requestingPlayerId && pf.FriendPlayerId == myPlayerId && pf.Status == FriendStatus.Pending, ct);
        if (request is null) return;

        db.PlayerFriends.Remove(request);
        await db.SaveChangesAsync(ct);
    }

    public async Task RemoveFriendAsync(int friendPlayerId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var myPlayerId = await GetMyPlayerIdAsync(db, ct);
        if (myPlayerId is null) return;

        var rel = await db.PlayerFriends.FirstOrDefaultAsync(pf =>
            pf.Status == FriendStatus.Accepted
            && ((pf.PlayerId == myPlayerId && pf.FriendPlayerId == friendPlayerId)
                || (pf.PlayerId == friendPlayerId && pf.FriendPlayerId == myPlayerId)), ct);
        if (rel is null) return;

        db.PlayerFriends.Remove(rel);
        await db.SaveChangesAsync(ct);
    }

    public async Task<bool> AreUsersFriendsAsync(string userAId, string userBId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var aPlayerIds = await db.Players.Where(p => p.UserId == userAId).Select(p => p.PlayerId).ToListAsync(ct);
        var bPlayerIds = await db.Players.Where(p => p.UserId == userBId).Select(p => p.PlayerId).ToListAsync(ct);
        if (aPlayerIds.Count == 0 || bPlayerIds.Count == 0) return false;

        return await db.PlayerFriends.AnyAsync(pf =>
            pf.Status == FriendStatus.Accepted &&
            ((aPlayerIds.Contains(pf.PlayerId) && bPlayerIds.Contains(pf.FriendPlayerId))
             || (aPlayerIds.Contains(pf.FriendPlayerId) && bPlayerIds.Contains(pf.PlayerId))), ct);
    }
}
