using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Saas.Billing.Domain.ValueObjects;

namespace Saas.Billing.Api.Endpoints;

internal static class ClaimsPrincipalExtensions
{
    public static bool TryGetActorContext(this ClaimsPrincipal user, out BillingActorContext actor)
    {
        actor = default!;

        var subClaim = user.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(subClaim, out var accountId) || accountId == Guid.Empty)
        {
            return false;
        }

        var roleClaim = user.FindFirstValue("role")
            ?? user.FindFirstValue(ClaimTypes.Role);
        if (string.IsNullOrWhiteSpace(roleClaim) ||
            int.TryParse(roleClaim.Trim(), out _) ||
            !Enum.TryParse<OrgRole>(roleClaim.Trim(), ignoreCase: true, out var role) ||
            !Enum.IsDefined(role))
        {
            return false;
        }

        Guid? tenantId = null;
        var tenantClaim = user.FindFirstValue("tenant_id");
        if (!string.IsNullOrWhiteSpace(tenantClaim))
        {
            if (!Guid.TryParse(tenantClaim.Trim(), out var parsedTenantId) || parsedTenantId == Guid.Empty)
            {
                return false;
            }

            tenantId = parsedTenantId;
        }

        var fullName = user.FindFirstValue("full_name")
            ?? user.FindFirstValue(ClaimTypes.Name)
            ?? "Usuario";

        actor = new BillingActorContext(accountId, tenantId, role, fullName);
        return true;
    }
}
