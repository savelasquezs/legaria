using System.Security.Claims;
using Legaria.API.Security;
using Legaria.Domain.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace Legaria.UnitTests;

public sealed class AuthorizationPolicyTests
{
    [Fact]
    public async Task PlatformOwnerPolicy_AllowsOnlyPlatformOwner()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorization(AuthorizationPolicies.Configure);
        await using var provider = services.BuildServiceProvider();
        var authorization = provider.GetRequiredService<IAuthorizationService>();
        var owner = Principal(AccountTypeCodes.Platform, PlatformRoleCodes.Owner);
        var platformAdmin = Principal(AccountTypeCodes.Platform, PlatformRoleCodes.PlatformAdmin);
        var tenantOwnerClaim = Principal(AccountTypeCodes.Tenant, PlatformRoleCodes.Owner);

        Assert.True((await authorization.AuthorizeAsync(
            owner,
            null,
            AuthorizationPolicies.PlatformOwnerOnly)).Succeeded);
        Assert.False((await authorization.AuthorizeAsync(
            platformAdmin,
            null,
            AuthorizationPolicies.PlatformOwnerOnly)).Succeeded);
        Assert.False((await authorization.AuthorizeAsync(
            tenantOwnerClaim,
            null,
            AuthorizationPolicies.PlatformOwnerOnly)).Succeeded);
        Assert.True((await authorization.AuthorizeAsync(
            owner,
            null,
            AuthorizationPolicies.PlatformAdminOrOwner)).Succeeded);
        Assert.True((await authorization.AuthorizeAsync(
            platformAdmin,
            null,
            AuthorizationPolicies.PlatformAdminOrOwner)).Succeeded);
        Assert.False((await authorization.AuthorizeAsync(
            tenantOwnerClaim,
            null,
            AuthorizationPolicies.PlatformAdminOrOwner)).Succeeded);
    }

    private static ClaimsPrincipal Principal(string accountType, string role) =>
        new(new ClaimsIdentity(
            [
                new Claim("account_type", accountType),
                new Claim(ClaimTypes.Role, role)
            ],
            "tests"));
}
