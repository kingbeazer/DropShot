using DropShot.Data;
using DropShot.Models;
using DropShot.Shared.Dtos;
using DropShot.UI.Services;
using DropShot.UI.Services.Auth;
using Microsoft.EntityFrameworkCore;

namespace DropShot.Services;

/// <summary>
/// Web implementation of <see cref="IClubService"/>. Mirrors
/// <c>ClubsController</c> read endpoints and the public-surface write paths.
/// The 14 admin-template / rules-management write paths stay web-only on the
/// page (via the <c>ClubAdminDialogs</c> render-fragment seam) until phase 5.
/// </summary>
public sealed class WebClubService(
    IDbContextFactory<MyDbContext> dbFactory,
    ICurrentUser currentUser) : IClubService
{
    public async Task<List<ClubDto>> GetClubsAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var clubs = await db.Clubs.Include(c => c.Courts)
            .OrderBy(c => c.Name)
            .ToListAsync(ct);
        return clubs.Select(c => ToDto(c)).ToList();
    }

    public async Task<ClubDetailDto?> GetClubAsync(int id, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var c = await db.Clubs.Include(x => x.Courts).FirstOrDefaultAsync(x => x.ClubId == id, ct);
        if (c is null) return null;

        return new ClubDetailDto(
            c.ClubId, c.Name, c.AddressLine1, c.AddressLine2,
            c.Town, c.Postcode, c.Phone, c.Email, c.Website,
            c.Courts.Select(co => new CourtDto(co.CourtId, co.ClubId, co.Name,
                (DropShot.Shared.CourtSurface)co.Surface, co.IsIndoor)).ToList());
    }

    public async Task<ClubDto> CreateClubAsync(SaveClubRequest request, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var club = ApplyTo(new Club(), request);
        db.Clubs.Add(club);
        await db.SaveChangesAsync(ct);
        return ToDto(club, courtCount: 0);
    }

    public async Task<ClubDto> UpdateClubAsync(int clubId, SaveClubRequest request, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var c = await db.Clubs.Include(x => x.Courts).FirstOrDefaultAsync(x => x.ClubId == clubId, ct)
            ?? throw new KeyNotFoundException($"Club {clubId} not found.");
        ApplyTo(c, request);
        await db.SaveChangesAsync(ct);
        return ToDto(c);
    }

    public async Task DeleteClubAsync(int clubId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var c = await db.Clubs.FindAsync([clubId], ct);
        if (c is null) return;
        db.Clubs.Remove(c);
        await db.SaveChangesAsync(ct);
    }

    public async Task<CourtDto> AddCourtAsync(int clubId, AddCourtRequest request, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var court = new Court
        {
            ClubId = clubId,
            Name = request.Name.Trim(),
            Surface = (DropShot.Models.CourtSurface)request.Surface,
            IsIndoor = request.IsIndoor
        };
        db.Courts.Add(court);
        await db.SaveChangesAsync(ct);
        return new CourtDto(court.CourtId, court.ClubId, court.Name,
            (DropShot.Shared.CourtSurface)court.Surface, court.IsIndoor);
    }

    public async Task<CourtDto> UpdateCourtAsync(int clubId, int courtId, UpdateCourtRequest request, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var court = await db.Courts.FindAsync([courtId], ct)
            ?? throw new KeyNotFoundException($"Court {courtId} not found.");
        if (court.ClubId != clubId)
            throw new InvalidOperationException("Court does not belong to this club.");
        court.Name = request.Name.Trim();
        court.Surface = (DropShot.Models.CourtSurface)request.Surface;
        court.IsIndoor = request.IsIndoor;
        await db.SaveChangesAsync(ct);
        return new CourtDto(court.CourtId, court.ClubId, court.Name,
            (DropShot.Shared.CourtSurface)court.Surface, court.IsIndoor);
    }

    public async Task DeleteCourtAsync(int clubId, int courtId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var court = await db.Courts.FindAsync([courtId], ct);
        if (court is null || court.ClubId != clubId) return;
        db.Courts.Remove(court);
        await db.SaveChangesAsync(ct);
    }

    public async Task<UserClubLinksDto> GetMyClubLinksAsync(CancellationToken ct = default)
    {
        var userId = currentUser.UserId;
        if (string.IsNullOrEmpty(userId))
            return new UserClubLinksDto([], [], []);

        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var adminIds = await db.ClubAdministrators
            .Where(ca => ca.UserId == userId)
            .Select(ca => ca.ClubId)
            .ToListAsync(ct);

        var playerId = await db.Players
            .Where(p => p.UserId == userId && !p.IsLight)
            .Select(p => (int?)p.PlayerId)
            .FirstOrDefaultAsync(ct);

        var linkedIds = playerId is int pid
            ? await db.ClubPlayers.Where(cp => cp.PlayerId == pid && cp.IsActive)
                .Select(cp => cp.ClubId).ToListAsync(ct)
            : new List<int>();

        var pendingIds = await db.ClubLinkRequests
            .Where(r => r.UserId == userId && r.Status == ClubLinkRequestStatus.Pending)
            .Select(r => r.ClubId)
            .ToListAsync(ct);

        return new UserClubLinksDto(adminIds, linkedIds, pendingIds);
    }

    public async Task RequestClubLinkAsync(int clubId, CancellationToken ct = default)
    {
        var userId = currentUser.UserId;
        if (string.IsNullOrEmpty(userId))
            throw new InvalidOperationException("Not authenticated.");

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var existing = await db.ClubLinkRequests
            .AnyAsync(r => r.UserId == userId && r.ClubId == clubId
                && r.Status == ClubLinkRequestStatus.Pending, ct);
        if (existing) return;

        db.ClubLinkRequests.Add(new ClubLinkRequest
        {
            ClubId = clubId,
            UserId = userId,
            Status = ClubLinkRequestStatus.Pending,
            RequestedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync(ct);
    }

    public async Task CancelMyClubLinkRequestAsync(int clubId, CancellationToken ct = default)
    {
        var userId = currentUser.UserId;
        if (string.IsNullOrEmpty(userId)) return;

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var pending = await db.ClubLinkRequests
            .Where(r => r.UserId == userId && r.ClubId == clubId
                && r.Status == ClubLinkRequestStatus.Pending)
            .ToListAsync(ct);
        if (pending.Count == 0) return;
        db.ClubLinkRequests.RemoveRange(pending);
        await db.SaveChangesAsync(ct);
    }

    private static Club ApplyTo(Club c, SaveClubRequest req)
    {
        c.Name = req.Name.Trim();
        c.AddressLine1 = NullIfEmpty(req.AddressLine1);
        c.AddressLine2 = NullIfEmpty(req.AddressLine2);
        c.Town = NullIfEmpty(req.Town);
        c.Postcode = NullIfEmpty(req.Postcode);
        c.Phone = NullIfEmpty(req.Phone);
        c.Email = NullIfEmpty(req.Email);
        c.Website = NullIfEmpty(req.Website);
        return c;
    }

    private static ClubDto ToDto(Club c) => new(
        c.ClubId, c.Name, c.AddressLine1, c.AddressLine2,
        c.Town, c.Postcode, c.Phone, c.Email, c.Website, c.Courts.Count);

    private static ClubDto ToDto(Club c, int courtCount) => new(
        c.ClubId, c.Name, c.AddressLine1, c.AddressLine2,
        c.Town, c.Postcode, c.Phone, c.Email, c.Website, courtCount);

    private static string? NullIfEmpty(string? s) =>
        string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
