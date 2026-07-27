using System.IdentityModel.Tokens.Jwt;
using Legaria.Application.Configuration;
using Legaria.Domain.Authentication;
using Legaria.Infrastructure.Authentication;

namespace Legaria.UnitTests;

public sealed class SecurityPrimitivesTests
{
    [Fact]
    public void EmailNormalizer_TrimsAndNormalizesInvariantly()
    {
        var normalizer = new EmailNormalizer();

        Assert.Equal("OWNER@LEGARIA.TEST", normalizer.Normalize("  Owner@Legaria.test "));
    }

    [Fact]
    public void SecureTokenService_GeneratesRandomTokenAndStoresOnlyStableHash()
    {
        var service = new SecureTokenService();

        var first = service.GenerateToken();
        var second = service.GenerateToken();

        Assert.NotEqual(first, second);
        Assert.DoesNotContain(first, service.HashToken(first), StringComparison.Ordinal);
        Assert.Equal(64, service.HashToken(first).Length);
        Assert.Equal(service.HashToken(first), service.HashToken(first));
    }

    [Fact]
    public void PasswordService_HashesAndVerifiesPassword()
    {
        var service = new PasswordService();
        const string password = "segura-123";

        var hash = service.Hash(password);

        Assert.NotEqual(password, hash);
        Assert.True(service.Verify(hash, password));
        Assert.False(service.Verify(hash, "incorrecta"));
    }

    [Fact]
    public void JwtForPlatform_DoesNotContainOrganizationClaim()
    {
        var clock = new FixedClock(new DateTimeOffset(2026, 7, 26, 12, 0, 0, TimeSpan.Zero));
        var service = new JwtAccessTokenService(
            new JwtOptions
            {
                Issuer = "legaria-tests",
                Audience = "legaria-tests",
                SigningKey = "tests-only-signing-key-with-at-least-32-bytes",
                AccessTokenMinutes = 10
            },
            clock);

        var token = service.Create(
            Guid.NewGuid(),
            AccountType.Platform,
            "stamp",
            [PlatformRoleCodes.Owner],
            null,
            null);
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token.Value);

        Assert.Equal(AccountTypeCodes.Platform, jwt.Claims.Single(x => x.Type == "account_type").Value);
        Assert.DoesNotContain(jwt.Claims, claim => claim.Type == "organization_id");
        Assert.Equal(clock.UtcNow.AddMinutes(10), token.ExpiresAt);
    }

    [Fact]
    public void AccountToken_CannotBeUsedTwiceOrAfterExpiration()
    {
        var now = new DateTimeOffset(2026, 7, 26, 12, 0, 0, TimeSpan.Zero);
        var expiredToken = AccountToken.Create(
            AccountType.Platform,
            Guid.NewGuid(),
            null,
            AccountTokenPurpose.PasswordReset,
            new string('b', 64),
            now.AddMinutes(30),
            now,
            "127.0.0.1");
        var token = AccountToken.Create(
            AccountType.Platform,
            Guid.NewGuid(),
            null,
            AccountTokenPurpose.PasswordReset,
            new string('a', 64),
            now.AddMinutes(30),
            now,
            "127.0.0.1");

        Assert.False(expiredToken.IsUsable(now.AddMinutes(30)));
        Assert.True(token.IsUsable(now));
        token.MarkUsed(now.AddMinutes(1));
        Assert.False(token.IsUsable(now.AddMinutes(2)));
    }

    private sealed class FixedClock(DateTimeOffset now) : Legaria.Application.Authentication.IClock
    {
        public DateTimeOffset UtcNow { get; } = now;
    }
}
