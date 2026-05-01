using DropShot.Data;
using DropShot.Shared.Dtos;
using DropShot.UI.Services;
using Microsoft.EntityFrameworkCore;

namespace DropShot.Services;

/// <summary>
/// Web implementation of <see cref="IClubService"/>. Mirrors
/// <c>ClubsController</c> read endpoints. Phase 3 seed: read surface only —
/// admin / link-request methods land alongside their phase 5 page moves.
/// </summary>
public sealed class WebClubService(IDbContextFactory<MyDbContext> dbFactory) : IClubService
{
    public async Task<List<ClubDto>> GetClubsAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var clubs = await db.Clubs.Include(c => c.Courts)
            .OrderBy(c => c.Name)
            .ToListAsync(ct);
        return clubs.Select(c => new ClubDto(
            c.ClubId, c.Name, c.AddressLine1, c.AddressLine2,
            c.Town, c.Postcode, c.Phone, c.Email, c.Website,
            c.Courts.Count)).ToList();
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
}
