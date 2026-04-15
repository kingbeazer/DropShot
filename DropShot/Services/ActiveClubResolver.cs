using DropShot.Data;
using Microsoft.EntityFrameworkCore;

namespace DropShot.Services;

/// <summary>
/// Resolves which club a ClubAdmin is currently "acting as" for.
/// Reads the ActiveClubId cookie and validates it against ClubAdministrator.
/// </summary>
public static class ActiveClubResolver
{
    public static async Task<int?> GetActiveClubIdAsync(
        MyDbContext db, string userId, HttpContext? httpContext)
    {
        var adminClubIds = await db.ClubAdministrators
            .Where(ca => ca.UserId == userId)
            .Select(ca => ca.ClubId)
            .ToListAsync();

        if (adminClubIds.Count == 0) return null;

        if (int.TryParse(httpContext?.Request.Cookies["ActiveClubId"], out var cookieClubId)
            && adminClubIds.Contains(cookieClubId))
        {
            return cookieClubId;
        }

        return adminClubIds[0];
    }
}
