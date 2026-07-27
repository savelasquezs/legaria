using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Legaria.Application.Authentication;
using Legaria.Application.Configuration;
using Legaria.Domain.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;

namespace Legaria.Infrastructure.Authentication;

public sealed class PasswordService : IPasswordService
{
    private readonly PasswordHasher<object> _passwordHasher = new();
    private readonly object _subject = new();
    private readonly string _dummyHash;

    public PasswordService()
    {
        _dummyHash = _passwordHasher.HashPassword(_subject, "Legaria dummy password");
    }

    public string Hash(string password) => _passwordHasher.HashPassword(_subject, password);

    public bool Verify(string passwordHash, string password) =>
        _passwordHasher.VerifyHashedPassword(_subject, passwordHash, password)
        is PasswordVerificationResult.Success or PasswordVerificationResult.SuccessRehashNeeded;

    public void VerifyUnknown(string password) =>
        _ = _passwordHasher.VerifyHashedPassword(_subject, _dummyHash, password);
}

public sealed class EmailNormalizer : IEmailNormalizer
{
    public string Normalize(string email) => email.Trim().ToUpper(CultureInfo.InvariantCulture);
}

public sealed class SecureTokenService : ISecureTokenService
{
    public string GenerateToken() =>
        Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(32));

    public string HashToken(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();

    public string GenerateSecurityStamp() =>
        Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
}

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}

public sealed class JwtAccessTokenService(JwtOptions options, IClock clock) : IAccessTokenService
{
    public AccessToken Create(
        Guid userId,
        AccountType accountType,
        string securityStamp,
        IReadOnlyCollection<string> roles,
        Guid? organizationId,
        Guid? employeeId)
    {
        var now = clock.UtcNow;
        var expiresAt = now.AddMinutes(options.AccessTokenMinutes);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new("account_type", accountType == AccountType.Platform
                ? AccountTypeCodes.Platform
                : AccountTypeCodes.Tenant),
            new("security_stamp", securityStamp),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(JwtRegisteredClaimNames.Iat, now.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64)
        };

        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));
        if (accountType == AccountType.Tenant)
        {
            claims.Add(new Claim("organization_id", organizationId!.Value.ToString()));
            if (employeeId is not null)
            {
                claims.Add(new Claim("employee_id", employeeId.Value.ToString()));
            }
        }

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SigningKey)),
            SecurityAlgorithms.HmacSha256);
        var jwt = new JwtSecurityToken(
            options.Issuer,
            options.Audience,
            claims,
            now.UtcDateTime,
            expiresAt.UtcDateTime,
            credentials);

        return new AccessToken(new JwtSecurityTokenHandler().WriteToken(jwt), expiresAt);
    }
}
