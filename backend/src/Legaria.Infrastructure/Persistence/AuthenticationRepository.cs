using Legaria.Application.Authentication;
using Legaria.Domain.Authentication;
using Microsoft.EntityFrameworkCore;

namespace Legaria.Infrastructure.Persistence;

public sealed class AuthenticationRepository(LegariaDbContext dbContext) : IAuthenticationRepository
{
    public Task<bool> AnyPlatformUserAsync(CancellationToken cancellationToken) =>
        dbContext.PlatformUsers.AnyAsync(cancellationToken);

    public Task<PlatformUser?> FindPlatformByEmailAsync(
        string normalizedEmail,
        CancellationToken cancellationToken) =>
        dbContext.PlatformUsers.SingleOrDefaultAsync(
            item => item.NormalizedEmail == normalizedEmail,
            cancellationToken);

    public Task<UserAccount?> FindTenantByEmailAsync(
        string normalizedEmail,
        CancellationToken cancellationToken) =>
        dbContext.UserAccounts
            .Include(item => item.Roles)
            .ThenInclude(item => item.SystemRole)
            .AsSplitQuery()
            .SingleOrDefaultAsync(item => item.NormalizedEmail == normalizedEmail, cancellationToken);

    public Task<PlatformUser?> FindPlatformByIdAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.PlatformUsers.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);

    public Task<UserAccount?> FindTenantByIdAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.UserAccounts
            .Include(item => item.Roles)
            .ThenInclude(item => item.SystemRole)
            .AsSplitQuery()
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);

    public Task<bool> IsOrganizationActiveAsync(Guid organizationId, CancellationToken cancellationToken) =>
        dbContext.Organizations.AnyAsync(
            item => item.Id == organizationId && item.Status == OrganizationStatus.Active,
            cancellationToken);

    public async Task<bool> EmailExistsAsync(string normalizedEmail, CancellationToken cancellationToken) =>
        await dbContext.PlatformUsers.AnyAsync(
            item => item.NormalizedEmail == normalizedEmail,
            cancellationToken) ||
        await dbContext.UserAccounts.AnyAsync(
            item => item.NormalizedEmail == normalizedEmail,
            cancellationToken);

    public Task<RefreshSession?> FindRefreshSessionAsync(
        string tokenHash,
        CancellationToken cancellationToken) =>
        dbContext.RefreshSessions.SingleOrDefaultAsync(
            item => item.TokenHash == tokenHash,
            cancellationToken);

    public Task<AccountToken?> FindAccountTokenAsync(
        string tokenHash,
        AccountTokenPurpose purpose,
        CancellationToken cancellationToken) =>
        dbContext.AccountTokens.SingleOrDefaultAsync(
            item => item.TokenHash == tokenHash && item.Purpose == purpose,
            cancellationToken);

    public async Task<IReadOnlyCollection<AccountToken>> FindActiveAccountTokensAsync(
        AccountType accountType,
        Guid accountId,
        AccountTokenPurpose purpose,
        CancellationToken cancellationToken)
    {
        return await dbContext.AccountTokens
            .Where(item =>
                item.AccountType == accountType &&
                item.Purpose == purpose &&
                item.UsedAt == null &&
                item.RevokedAt == null &&
                (accountType == AccountType.Platform
                    ? item.PlatformUserId == accountId
                    : item.UserAccountId == accountId))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<RefreshSession>> FindSessionsByFamilyAsync(
        Guid familyId,
        CancellationToken cancellationToken) =>
        await dbContext.RefreshSessions
            .Where(item => item.FamilyId == familyId)
            .ToArrayAsync(cancellationToken);

    public async Task<IReadOnlyCollection<RefreshSession>> FindActiveSessionsAsync(
        AccountType accountType,
        Guid accountId,
        DateTimeOffset now,
        CancellationToken cancellationToken) =>
        await dbContext.RefreshSessions
            .Where(item =>
                item.RevokedAt == null &&
                item.ExpiresAt > now &&
                (accountType == AccountType.Platform
                    ? item.PlatformUserId == accountId
                    : item.UserAccountId == accountId))
            .ToArrayAsync(cancellationToken);

    public void AddPlatformUser(PlatformUser platformUser) =>
        dbContext.PlatformUsers.Add(platformUser);

    public void AddRefreshSession(RefreshSession refreshSession) =>
        dbContext.RefreshSessions.Add(refreshSession);

    public void AddAccountToken(AccountToken accountToken) =>
        dbContext.AccountTokens.Add(accountToken);

    public void AddAuditEvent(SecurityAuditEvent auditEvent) =>
        dbContext.SecurityAuditEvents.Add(auditEvent);

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
