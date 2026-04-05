using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace DropShot.Authorization;

/// <summary>
/// Action filter that checks the active role from the JWT's "active_role" claim.
/// Use on API controllers instead of [Authorize(Roles=...)] when you want to
/// enforce role checks against the active role specifically.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class RequireActiveRoleAttribute(params string[] roles) : Attribute, IAuthorizationFilter
{
    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var user = context.HttpContext.User;
        if (user.Identity?.IsAuthenticated != true)
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        var activeRole = user.FindFirst("active_role")?.Value;

        // Fall back to standard role claim if no active_role claim present
        // (e.g., tokens issued before this feature was deployed)
        if (string.IsNullOrEmpty(activeRole))
        {
            activeRole = user.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
        }

        if (string.IsNullOrEmpty(activeRole) ||
            !roles.Any(r => r.Equals(activeRole, StringComparison.OrdinalIgnoreCase)))
        {
            context.Result = new ForbidResult();
        }
    }
}
