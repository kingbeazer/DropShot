using DropShot.Data;
using DropShot.Models;
using Microsoft.EntityFrameworkCore;

namespace DropShot.Services;

/// <summary>
/// Handles the server-side logic for role switching in Blazor Server circuits.
/// Coordinates between ActiveRoleService (in-memory state) and the database (audit log).
/// </summary>
public class RoleSwitchService(
    ActiveRoleService activeRoleService,
    IDbContextFactory<MyDbContext> dbFactory,
    ILogger<RoleSwitchService> logger)
{
    public async Task<bool> SwitchRoleAsync(string userId, string newRole, string? ipAddress)
    {
        var previousRole = activeRoleService.ActiveRole;

        if (!activeRoleService.TrySwitch(newRole))
            return false;

        activeRoleService.PreviousRole = previousRole;

        // Log to database
        await using var db = dbFactory.CreateDbContext();
        db.RoleSwitchLogs.Add(new RoleSwitchLog
        {
            UserId = userId,
            FromRole = previousRole,
            ToRole = newRole,
            Timestamp = DateTime.UtcNow,
            IpAddress = ipAddress
        });
        await db.SaveChangesAsync();

        logger.LogInformation(
            "Role switch: User {UserId} switched from {FromRole} to {ToRole} (IP: {Ip})",
            userId, previousRole, newRole, ipAddress);

        return true;
    }
}
