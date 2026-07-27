using Legaria.Domain.Authentication;
using Microsoft.AspNetCore.Authorization;

namespace Legaria.API.Security;

public static class AuthorizationPolicies
{
    public const string PlatformOwnerOnly = nameof(PlatformOwnerOnly);
    public const string PlatformAdminOrOwner = nameof(PlatformAdminOrOwner);

    public static void Configure(AuthorizationOptions options)
    {
        options.AddPolicy(
            PlatformOwnerOnly,
            policy => policy
                .RequireAuthenticatedUser()
                .RequireClaim("account_type", AccountTypeCodes.Platform)
                .RequireRole(PlatformRoleCodes.Owner));
        options.AddPolicy(
            PlatformAdminOrOwner,
            policy => policy
                .RequireAuthenticatedUser()
                .RequireClaim("account_type", AccountTypeCodes.Platform)
                .RequireRole(PlatformRoleCodes.Owner, PlatformRoleCodes.PlatformAdmin));
    }
}
