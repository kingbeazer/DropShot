using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using DropShot.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;

namespace DropShot.Services;

public class JwtTokenService(IConfiguration config, UserManager<ApplicationUser> userManager)
{
    /// <summary>
    /// Generate a JWT with all the user's roles (default behavior).
    /// The first role in the list is set as the active role.
    /// </summary>
    public async Task<string> GenerateTokenAsync(ApplicationUser user)
    {
        var roles = await userManager.GetRolesAsync(user);
        var activeRole = roles.FirstOrDefault() ?? "";
        return GenerateTokenWithActiveRole(user, roles, activeRole);
    }

    /// <summary>
    /// Generate a JWT with a specific active role. The token carries all granted
    /// roles in "granted_role" claims but only the active role in the standard
    /// Role claim, so [Authorize(Roles=...)] checks respect the active role.
    /// </summary>
    public string GenerateTokenWithActiveRole(
        ApplicationUser user, IList<string> grantedRoles, string activeRole)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id),
            new(JwtRegisteredClaimNames.Email, user.Email ?? ""),
            new(JwtRegisteredClaimNames.UniqueName, user.UserName ?? ""),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(ClaimTypes.Role, activeRole),
            new("active_role", activeRole)
        };

        // Store all granted roles as separate claims for reference
        claims.AddRange(grantedRoles.Select(r => new Claim("granted_role", r)));

        var jwtCfg = config.GetSection("Jwt");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtCfg["Key"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expiry = DateTime.UtcNow.AddHours(double.Parse(jwtCfg["ExpiryHours"] ?? "24"));

        var token = new JwtSecurityToken(
            issuer: jwtCfg["Issuer"],
            audience: jwtCfg["Audience"],
            claims: claims,
            expires: expiry,
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
