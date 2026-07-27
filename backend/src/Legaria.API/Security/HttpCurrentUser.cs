using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Legaria.Application.Authentication;
using Legaria.Domain.Authentication;

namespace Legaria.API.Security;

public sealed class HttpCurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    private ClaimsPrincipal Principal =>
        httpContextAccessor.HttpContext?.User
        ?? throw new InvalidOperationException("No existe un contexto HTTP activo.");

    public bool IsAuthenticated => Principal.Identity?.IsAuthenticated == true;

    public Guid UserId => ParseRequiredGuid(JwtRegisteredClaimNames.Sub);

    public AccountType AccountType => Principal.FindFirstValue("account_type") switch
    {
        AccountTypeCodes.Platform => AccountType.Platform,
        AccountTypeCodes.Tenant => AccountType.Tenant,
        _ => throw new InvalidOperationException("El token no contiene un tipo de cuenta válido.")
    };

    public bool IsPlatformUser => AccountType == AccountType.Platform;
    public bool IsTenantUser => AccountType == AccountType.Tenant;
    public Guid? OrganizationId => ParseOptionalGuid("organization_id");
    public Guid? EmployeeId => ParseOptionalGuid("employee_id");
    public IReadOnlyCollection<string> Roles =>
        Principal.FindAll(ClaimTypes.Role).Select(claim => claim.Value).ToArray();

    public CurrentAccount ToCurrentAccount() =>
        new(UserId, AccountType, OrganizationId, EmployeeId, Roles);

    private Guid ParseRequiredGuid(string claimType)
    {
        var value = Principal.FindFirstValue(claimType);
        return Guid.TryParse(value, out var parsed)
            ? parsed
            : throw new InvalidOperationException($"El claim {claimType} no es válido.");
    }

    private Guid? ParseOptionalGuid(string claimType)
    {
        var value = Principal.FindFirstValue(claimType);
        return string.IsNullOrWhiteSpace(value)
            ? null
            : Guid.TryParse(value, out var parsed)
                ? parsed
                : throw new InvalidOperationException($"El claim {claimType} no es válido.");
    }
}
