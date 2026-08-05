using Legaria.Domain.Authentication;
using Microsoft.AspNetCore.Authorization;

namespace Legaria.API.Security;

public static class AuthorizationPolicies
{
    public const string PlatformOwnerOnly = nameof(PlatformOwnerOnly);
    public const string PlatformAdminOrOwner = nameof(PlatformAdminOrOwner);
    public const string AuthenticatedAccount = nameof(AuthenticatedAccount);
    public const string TenantAdministrator = nameof(TenantAdministrator);
    public const string TenantSuperAdministrator = nameof(TenantSuperAdministrator);

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
        options.AddPolicy(
            AuthenticatedAccount,
            policy => policy.RequireAuthenticatedUser());
        options.AddPolicy(
            TenantAdministrator,
            policy => policy
                .RequireAuthenticatedUser()
                .RequireClaim("account_type", AccountTypeCodes.Tenant)
                .RequireRole(SystemRoleCodes.SuperAdmin, SystemRoleCodes.BranchAdmin));
        options.AddPolicy(
            TenantSuperAdministrator,
            policy => policy
                .RequireAuthenticatedUser()
                .RequireClaim("account_type", AccountTypeCodes.Tenant)
                .RequireRole(SystemRoleCodes.SuperAdmin));
    }
}
