using System.IdentityModel.Tokens.Jwt;
using Legaria.Application.Authentication;
using Legaria.Application.Configuration;
using Legaria.Domain.Authentication;
using Legaria.Domain.Employees;
using Legaria.Domain.Tenancy;
using Legaria.Infrastructure.Authentication;
using Legaria.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Legaria.IntegrationTests;

[Collection(PostgreSqlCollection.Name)]
public sealed class AuthenticationIntegrationTests(PostgreSqlFixture fixture)
{
    [Fact]
    public async Task Bootstrap_CreatesOneVerifiedOwnerAndDoesNotDuplicateIt()
    {
        await fixture.ResetAsync();
        await using var context = fixture.CreateDbContext();
        var services = CreateServices(context);
        var bootstrapper = new PlatformOwnerBootstrapper(
            services.Repository,
            services.Normalizer,
            services.Passwords,
            services.Tokens,
            services.Clock,
            OwnerOptions());

        await bootstrapper.BootstrapAsync(CancellationToken.None);
        await bootstrapper.BootstrapAsync(CancellationToken.None);

        var owner = Assert.Single(await context.PlatformUsers.ToArrayAsync());
        Assert.Equal(PlatformRole.Owner, owner.Role);
        Assert.NotNull(owner.EmailVerifiedAt);
        Assert.True(services.Passwords.Verify(owner.PasswordHash, "bootstrap-123"));
        Assert.DoesNotContain("bootstrap-123", owner.PasswordHash, StringComparison.Ordinal);
        Assert.Single(await context.SecurityAuditEvents
            .Where(item => item.EventType == "PLATFORM_OWNER_BOOTSTRAPPED")
            .ToArrayAsync());
    }

    [Fact]
    public async Task PlatformLoginAndRefresh_RotateTokenAndRevokeFamilyOnReuse()
    {
        await fixture.ResetAsync();
        await using var context = fixture.CreateDbContext();
        var services = CreateServices(context);
        await BootstrapAsync(services);

        var login = await services.Authentication.LoginAsync(
            new LoginRequest("OWNER@LEGARIA.TEST", "bootstrap-123"),
            Client(),
            CancellationToken.None);
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(login.AccessToken);

        Assert.Equal(AccountTypeCodes.Platform, login.Account.AccountType);
        Assert.DoesNotContain(jwt.Claims, claim => claim.Type == "organization_id");
        var stored = Assert.Single(await context.RefreshSessions.ToArrayAsync());
        Assert.NotEqual(login.RefreshToken, stored.TokenHash);

        var rotated = await services.Authentication.RefreshAsync(
            login.RefreshToken,
            Client(),
            CancellationToken.None);
        Assert.NotEqual(login.RefreshToken, rotated.RefreshToken);

        var reuse = await Assert.ThrowsAsync<AuthException>(() =>
            services.Authentication.RefreshAsync(login.RefreshToken, Client(), CancellationToken.None));
        Assert.Equal(AuthErrorCodes.InvalidRefreshToken, reuse.Code);
        Assert.All(await context.RefreshSessions.ToArrayAsync(), session => Assert.NotNull(session.RevokedAt));
        Assert.Single(await context.SecurityAuditEvents
            .Where(item => item.EventType == "REFRESH_TOKEN_REUSE_DETECTED")
            .ToArrayAsync());
    }

    [Fact]
    public async Task PlatformLogin_RejectsSuspensionUnverifiedEmailAndLocksAfterFiveFailures()
    {
        await fixture.ResetAsync();
        await using var context = fixture.CreateDbContext();
        var services = CreateServices(context);
        await BootstrapAsync(services);
        var owner = await context.PlatformUsers.SingleAsync();

        context.Entry(owner).Property(item => item.Status).CurrentValue = AccountStatus.Suspended;
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        var suspended = await Assert.ThrowsAsync<AuthException>(() =>
            services.Authentication.LoginAsync(
                new LoginRequest("owner@legaria.test", "bootstrap-123"),
                Client(),
                CancellationToken.None));
        Assert.Equal(AuthErrorCodes.AccountUnavailable, suspended.Code);

        owner = await context.PlatformUsers.SingleAsync();
        context.Entry(owner).Property(item => item.Status).CurrentValue = AccountStatus.Active;
        context.Entry(owner).Property(item => item.EmailVerifiedAt).CurrentValue = null;
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        var unverified = await Assert.ThrowsAsync<AuthException>(() =>
            services.Authentication.LoginAsync(
                new LoginRequest("owner@legaria.test", "bootstrap-123"),
                Client(),
                CancellationToken.None));
        Assert.Equal(AuthErrorCodes.EmailNotVerified, unverified.Code);

        owner = await context.PlatformUsers.SingleAsync();
        context.Entry(owner).Property(item => item.EmailVerifiedAt).CurrentValue = services.Clock.UtcNow;
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var invalid = await Assert.ThrowsAsync<AuthException>(() =>
                services.Authentication.LoginAsync(
                    new LoginRequest("owner@legaria.test", "incorrecta"),
                    Client(),
                    CancellationToken.None));
            Assert.Equal(AuthErrorCodes.InvalidCredentials, invalid.Code);
        }

        var locked = await Assert.ThrowsAsync<AuthException>(() =>
            services.Authentication.LoginAsync(
                new LoginRequest("owner@legaria.test", "bootstrap-123"),
                Client(),
                CancellationToken.None));
        Assert.Equal(AuthErrorCodes.AccountLocked, locked.Code);
        owner = await context.PlatformUsers.SingleAsync();
        Assert.Equal(services.Clock.UtcNow.AddMinutes(15), owner.LockoutEndAt);
    }

    [Fact]
    public async Task LogoutAndLogoutAll_RevokeSessionsRotateStampAndWriteAudit()
    {
        await fixture.ResetAsync();
        await using var context = fixture.CreateDbContext();
        var services = CreateServices(context);
        await BootstrapAsync(services);
        var first = await services.Authentication.LoginAsync(
            new LoginRequest("owner@legaria.test", "bootstrap-123"),
            Client(),
            CancellationToken.None);

        await services.Authentication.LogoutAsync(first.RefreshToken, Client(), CancellationToken.None);
        Assert.NotNull((await context.RefreshSessions.SingleAsync()).RevokedAt);

        var second = await services.Authentication.LoginAsync(
            new LoginRequest("owner@legaria.test", "bootstrap-123"),
            Client(),
            CancellationToken.None);
        var previousStamp = (await context.PlatformUsers.SingleAsync()).SecurityStamp;
        await services.Authentication.LogoutAllAsync(
            new CurrentAccount(
                second.Account.Id,
                AccountType.Platform,
                null,
                null,
                second.Account.Roles),
            Client(),
            CancellationToken.None);

        var owner = await context.PlatformUsers.SingleAsync();
        Assert.NotEqual(previousStamp, owner.SecurityStamp);
        Assert.All(await context.RefreshSessions.ToArrayAsync(), session => Assert.NotNull(session.RevokedAt));
        var eventTypes = await context.SecurityAuditEvents.Select(item => item.EventType).ToArrayAsync();
        Assert.Contains("LOGOUT", eventTypes);
        Assert.Contains("LOGOUT_ALL", eventTypes);
    }

    [Fact]
    public async Task PasswordReset_UsesLatestHashedTokenAndRevokesSessions()
    {
        await fixture.ResetAsync();
        await using var context = fixture.CreateDbContext();
        var services = CreateServices(context);
        await BootstrapAsync(services);
        var login = await services.Authentication.LoginAsync(
            new LoginRequest("owner@legaria.test", "bootstrap-123"),
            Client(),
            CancellationToken.None);
        var originalStamp = (await context.PlatformUsers.SingleAsync()).SecurityStamp;

        await services.Authentication.RequestPasswordResetAsync(
            "owner@legaria.test",
            Client(),
            CancellationToken.None);
        var firstToken = ExtractToken(services.Email.LastHtml);
        await services.Authentication.RequestPasswordResetAsync(
            "owner@legaria.test",
            Client(),
            CancellationToken.None);
        var secondToken = ExtractToken(services.Email.LastHtml);

        var replaced = await Assert.ThrowsAsync<AuthException>(() =>
            services.Authentication.ResetPasswordAsync(
                new ResetPasswordRequest(firstToken, "nueva-clave-123"),
                Client(),
                CancellationToken.None));
        Assert.Equal(AuthErrorCodes.UsedToken, replaced.Code);

        await services.Authentication.ResetPasswordAsync(
            new ResetPasswordRequest(secondToken, "nueva-clave-123"),
            Client(),
            CancellationToken.None);

        var owner = await context.PlatformUsers.SingleAsync();
        Assert.NotEqual(originalStamp, owner.SecurityStamp);
        Assert.True(services.Passwords.Verify(owner.PasswordHash, "nueva-clave-123"));
        Assert.All(await context.RefreshSessions.ToArrayAsync(), session => Assert.NotNull(session.RevokedAt));
        var oldRefresh = await Assert.ThrowsAsync<AuthException>(() =>
            services.Authentication.RefreshAsync(login.RefreshToken, Client(), CancellationToken.None));
        Assert.Equal(AuthErrorCodes.InvalidRefreshToken, oldRefresh.Code);
    }

    [Fact]
    public async Task EmailVerification_UsesLatestTokenOnceAndWritesAudit()
    {
        await fixture.ResetAsync();
        await using var context = fixture.CreateDbContext();
        var services = CreateServices(context);
        var now = services.Clock.UtcNow;
        var organization = Organization.Create("Tenant de verificacion", now);
        var tenant = UserAccount.Create(
            organization.Id,
            null,
            "verify@tenant.test",
            services.Normalizer.Normalize("verify@tenant.test"),
            services.Passwords.Hash("tenant-123"),
            "Ana",
            "Prueba",
            services.Tokens.GenerateSecurityStamp(),
            false,
            now);
        tenant.AddRole(SystemRole.SuperAdminId);
        context.AddRange(organization, tenant);
        await context.SaveChangesAsync();

        await services.Authentication.RequestEmailVerificationAsync(
            tenant.Email,
            Client(),
            CancellationToken.None);
        var rawToken = ExtractToken(services.Email.LastHtml);
        var storedToken = await context.AccountTokens.SingleAsync();
        Assert.NotEqual(rawToken, storedToken.TokenHash);

        await services.Authentication.VerifyEmailAsync(rawToken, Client(), CancellationToken.None);
        Assert.NotNull((await context.UserAccounts.SingleAsync()).EmailVerifiedAt);
        Assert.NotNull((await context.AccountTokens.SingleAsync()).UsedAt);
        Assert.Contains(
            await context.SecurityAuditEvents.Select(item => item.EventType).ToArrayAsync(),
            eventType => eventType == "EMAIL_VERIFIED");

        var reused = await Assert.ThrowsAsync<AuthException>(() =>
            services.Authentication.VerifyEmailAsync(rawToken, Client(), CancellationToken.None));
        Assert.Equal(AuthErrorCodes.UsedToken, reused.Code);
    }

    [Fact]
    public async Task TenantLogin_PreservesOrganizationClaimWhilePlatformHasNoTenantContext()
    {
        await fixture.ResetAsync();
        await using var context = fixture.CreateDbContext();
        var services = CreateServices(context);
        await BootstrapAsync(services);
        var now = services.Clock.UtcNow;
        var organization = Organization.Create("Organización A", now);
        var employee = Employee.Create(
            organization.Id,
            "CC",
            "10001",
            "Ana",
            "Prueba",
            now);
        var tenant = UserAccount.Create(
            organization.Id,
            employee.Id,
            "ana@tenant.test",
            services.Normalizer.Normalize("ana@tenant.test"),
            services.Passwords.Hash("tenant-123"),
            "Ana",
            "Prueba",
            services.Tokens.GenerateSecurityStamp(),
            true,
            now);
        tenant.AddRole(SystemRole.SuperAdminId);
        context.AddRange(organization, employee, tenant);
        await context.SaveChangesAsync();

        var result = await services.Authentication.LoginAsync(
            new LoginRequest("ana@tenant.test", "tenant-123"),
            Client(),
            CancellationToken.None);
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(result.AccessToken);

        Assert.Equal(AccountTypeCodes.Tenant, result.Account.AccountType);
        Assert.Equal(organization.Id, result.Account.OrganizationId);
        Assert.Equal(
            organization.Id.ToString(),
            jwt.Claims.Single(claim => claim.Type == "organization_id").Value);
    }

    [Fact]
    public async Task Database_RejectsRefreshSessionWithoutExactlyOneAccount()
    {
        await fixture.ResetAsync();
        await using var context = fixture.CreateDbContext();
        context.RefreshSessions.Add(RefreshSession.Create(
            null,
            null,
            Guid.NewGuid(),
            new string('a', 64),
            DateTimeOffset.UtcNow.AddDays(7),
            DateTimeOffset.UtcNow,
            null,
            null));

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task Migration_CreatesRequiredHashEmailIndexesAndAccountConstraints()
    {
        await fixture.ResetAsync();
        await using var context = fixture.CreateDbContext();

        var indexes = await context.Database.SqlQueryRaw<string>(
                """SELECT indexname AS "Value" FROM pg_indexes WHERE schemaname = 'public'""")
            .ToArrayAsync();
        Assert.Contains("ix_platform_users_normalized_email", indexes);
        Assert.Contains("ix_user_accounts_normalized_email", indexes);
        Assert.Contains("ix_refresh_sessions_token_hash", indexes);
        Assert.Contains("ix_account_tokens_token_hash", indexes);

        var constraints = await context.Database.SqlQueryRaw<string>(
                """
                SELECT conname AS "Value"
                FROM pg_constraint
                WHERE connamespace = 'public'::regnamespace
                """)
            .ToArrayAsync();
        Assert.Contains("ck_refresh_sessions_single_account", constraints);
        Assert.Contains("ck_account_tokens_single_account", constraints);
        Assert.Contains("ck_account_tokens_account_type", constraints);
    }

    private static async Task BootstrapAsync(TestServices services)
    {
        var bootstrapper = new PlatformOwnerBootstrapper(
            services.Repository,
            services.Normalizer,
            services.Passwords,
            services.Tokens,
            services.Clock,
            OwnerOptions());
        await bootstrapper.BootstrapAsync(CancellationToken.None);
    }

    private static TestServices CreateServices(LegariaDbContext context)
    {
        var repository = new AuthenticationRepository(context);
        var passwords = new PasswordService();
        var normalizer = new EmailNormalizer();
        var tokens = new SecureTokenService();
        var clock = new FixedClock(new DateTimeOffset(2026, 7, 26, 12, 0, 0, TimeSpan.Zero));
        var email = new CapturingEmailSender();
        var authenticationOptions = new Legaria.Application.Configuration.AuthenticationOptions();
        var frontendOptions = new FrontendOptions { BaseUrl = "https://legaria.test" };
        var jwt = new JwtAccessTokenService(
            new JwtOptions
            {
                Issuer = "legaria-tests",
                Audience = "legaria-tests",
                SigningKey = "tests-only-signing-key-with-at-least-32-bytes",
                AccessTokenMinutes = 10
            },
            clock);
        var authentication = new AuthenticationService(
            repository,
            passwords,
            normalizer,
            tokens,
            jwt,
            email,
            new PassThroughRenderer(),
            clock,
            authenticationOptions,
            frontendOptions);
        return new TestServices(
            repository,
            passwords,
            normalizer,
            tokens,
            clock,
            email,
            authentication);
    }

    private static BootstrapOwnerOptions OwnerOptions() =>
        new()
        {
            Email = "owner@legaria.test",
            Password = "bootstrap-123",
            FirstName = "Propietario",
            LastName = "Legaria"
        };

    private static ClientContext Client() => new("127.0.0.1", "integration-tests");

    private static string ExtractToken(string html)
    {
        var uri = new Uri(html);
        var value = uri.Query.TrimStart('=').Replace("token=", string.Empty, StringComparison.Ordinal);
        return Uri.UnescapeDataString(value);
    }

    private sealed record TestServices(
        AuthenticationRepository Repository,
        PasswordService Passwords,
        EmailNormalizer Normalizer,
        SecureTokenService Tokens,
        FixedClock Clock,
        CapturingEmailSender Email,
        AuthenticationService Authentication);

    private sealed class FixedClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow { get; } = now;
    }

    private sealed class CapturingEmailSender : IEmailSender
    {
        public string LastHtml { get; private set; } = string.Empty;

        public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
        {
            LastHtml = message.HtmlBody;
            return Task.CompletedTask;
        }
    }

    private sealed class PassThroughRenderer : IEmailTemplateRenderer
    {
        public string RenderVerification(string firstName, string verificationUrl, TimeSpan expiration) =>
            verificationUrl;

        public string RenderPasswordReset(string firstName, string resetUrl, TimeSpan expiration) =>
            resetUrl;
    }
}
