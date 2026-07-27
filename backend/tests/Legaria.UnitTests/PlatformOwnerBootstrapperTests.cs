using Legaria.Application.Authentication;
using Legaria.Application.Configuration;
using Legaria.Domain.Authentication;
using Legaria.Infrastructure.Authentication;

namespace Legaria.UnitTests;

public sealed class PlatformOwnerBootstrapperTests
{
    [Fact]
    public async Task CreatesOnePreverifiedOwnerAndSkipsConfigurationAfterwards()
    {
        var repository = new BootstrapRepository();
        var clock = new FixedClock(new DateTimeOffset(2026, 7, 26, 12, 0, 0, TimeSpan.Zero));
        var passwords = new PasswordService();
        var bootstrapper = new PlatformOwnerBootstrapper(
            repository,
            new EmailNormalizer(),
            passwords,
            new SecureTokenService(),
            clock,
            new BootstrapOwnerOptions
            {
                Email = "Owner@Legaria.test",
                Password = "bootstrap-123",
                FirstName = "Propietario",
                LastName = "Legaria"
            });

        await bootstrapper.BootstrapAsync(CancellationToken.None);

        var owner = Assert.Single(repository.PlatformUsers);
        Assert.Equal(PlatformRole.Owner, owner.Role);
        Assert.Equal(AccountStatus.Active, owner.Status);
        Assert.Equal(clock.UtcNow, owner.EmailVerifiedAt);
        Assert.Equal("OWNER@LEGARIA.TEST", owner.NormalizedEmail);
        Assert.True(passwords.Verify(owner.PasswordHash, "bootstrap-123"));
        Assert.Equal("PLATFORM_OWNER_BOOTSTRAPPED", Assert.Single(repository.AuditEvents).EventType);
        Assert.Equal(1, repository.SaveCount);

        var secondBootstrapper = new PlatformOwnerBootstrapper(
            repository,
            new EmailNormalizer(),
            passwords,
            new SecureTokenService(),
            clock,
            new BootstrapOwnerOptions());
        await secondBootstrapper.BootstrapAsync(CancellationToken.None);

        Assert.Single(repository.PlatformUsers);
        Assert.Equal(1, repository.SaveCount);
    }

    private sealed class FixedClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow { get; } = now;
    }

    private sealed class BootstrapRepository : IAuthenticationRepository
    {
        public List<PlatformUser> PlatformUsers { get; } = [];
        public List<SecurityAuditEvent> AuditEvents { get; } = [];
        public int SaveCount { get; private set; }

        public Task<bool> AnyPlatformUserAsync(CancellationToken cancellationToken) =>
            Task.FromResult(PlatformUsers.Count > 0);

        public Task<bool> EmailExistsAsync(string normalizedEmail, CancellationToken cancellationToken) =>
            Task.FromResult(PlatformUsers.Any(user => user.NormalizedEmail == normalizedEmail));

        public void AddPlatformUser(PlatformUser platformUser) => PlatformUsers.Add(platformUser);

        public void AddAuditEvent(SecurityAuditEvent auditEvent) => AuditEvents.Add(auditEvent);

        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            SaveCount++;
            return Task.CompletedTask;
        }

        public Task<PlatformUser?> FindPlatformByEmailAsync(
            string normalizedEmail,
            CancellationToken cancellationToken) =>
            Task.FromResult<PlatformUser?>(null);

        public Task<UserAccount?> FindTenantByEmailAsync(
            string normalizedEmail,
            CancellationToken cancellationToken) =>
            Task.FromResult<UserAccount?>(null);

        public Task<PlatformUser?> FindPlatformByIdAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult<PlatformUser?>(null);

        public Task<UserAccount?> FindTenantByIdAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult<UserAccount?>(null);

        public Task<bool> IsOrganizationActiveAsync(
            Guid organizationId,
            CancellationToken cancellationToken) =>
            Task.FromResult(false);

        public Task<RefreshSession?> FindRefreshSessionAsync(
            string tokenHash,
            CancellationToken cancellationToken) =>
            Task.FromResult<RefreshSession?>(null);

        public Task<AccountToken?> FindAccountTokenAsync(
            string tokenHash,
            AccountTokenPurpose purpose,
            CancellationToken cancellationToken) =>
            Task.FromResult<AccountToken?>(null);

        public Task<IReadOnlyCollection<AccountToken>> FindActiveAccountTokensAsync(
            AccountType accountType,
            Guid accountId,
            AccountTokenPurpose purpose,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyCollection<AccountToken>>([]);

        public Task<IReadOnlyCollection<RefreshSession>> FindSessionsByFamilyAsync(
            Guid familyId,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyCollection<RefreshSession>>([]);

        public Task<IReadOnlyCollection<RefreshSession>> FindActiveSessionsAsync(
            AccountType accountType,
            Guid accountId,
            DateTimeOffset now,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyCollection<RefreshSession>>([]);

        public void AddRefreshSession(RefreshSession refreshSession)
        {
        }

        public void AddAccountToken(AccountToken accountToken)
        {
        }
    }
}
