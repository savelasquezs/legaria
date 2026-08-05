using Legaria.Application.Authentication;
using Legaria.Application.Organizations;
using Legaria.Domain.Authentication;
using Legaria.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace Legaria.Infrastructure.Persistence;

public sealed class TenantInvitationRepository(LegariaDbContext dbContext) : ITenantInvitationRepository
{
    public Task<AccountToken?> FindByHashAsync(string tokenHash, CancellationToken cancellationToken) =>
        dbContext.AccountTokens.SingleOrDefaultAsync(
            item => item.TokenHash == tokenHash && item.Purpose == AccountTokenPurpose.TenantInvitation,
            cancellationToken);

    public Task<UserAccount?> FindAccountAsync(Guid accountId, CancellationToken cancellationToken) =>
        dbContext.UserAccounts.SingleOrDefaultAsync(item => item.Id == accountId, cancellationToken);

    public Task<Organization?> FindOrganizationAsync(Guid organizationId, CancellationToken cancellationToken) =>
        dbContext.Organizations.SingleOrDefaultAsync(item => item.Id == organizationId, cancellationToken);

    public async Task<IReadOnlyCollection<AccountToken>> FindActiveAsync(
        Guid accountId,
        CancellationToken cancellationToken) =>
        await dbContext.AccountTokens
            .Where(item =>
                item.UserAccountId == accountId &&
                item.Purpose == AccountTokenPurpose.TenantInvitation &&
                item.UsedAt == null &&
                item.RevokedAt == null)
            .ToArrayAsync(cancellationToken);

    public void AddToken(AccountToken token) => dbContext.AccountTokens.Add(token);
    public void AddAuditEvent(SecurityAuditEvent auditEvent) => dbContext.SecurityAuditEvents.Add(auditEvent);
    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new OrganizationException(
                OrganizationErrorCodes.UsedInvitation,
                "La invitación ya fue utilizada o reemplazada.",
                OrganizationErrorKind.Conflict);
        }
    }
}
