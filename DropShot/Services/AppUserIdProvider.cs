using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;

namespace DropShot.Services;

/// <summary>
/// Maps a hub connection's ClaimsPrincipal to the ApplicationUser id so
/// Clients.User(userId) can be used for private push. Cookie auth (web) sets
/// NameIdentifier directly; JWT bearer (MAUI) carries the id under "sub",
/// which isn't auto-mapped to NameIdentifier here — same fallback chain as
/// <see cref="WebCurrentUser"/>.
/// </summary>
public sealed class AppUserIdProvider : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection) =>
        connection.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
        ?? connection.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
        ?? connection.User.FindFirst("sub")?.Value;
}
