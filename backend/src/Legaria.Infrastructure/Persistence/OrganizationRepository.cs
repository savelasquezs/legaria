using Legaria.Application.Organizations;
using Legaria.Domain.Authentication;
using Legaria.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Legaria.Infrastructure.Persistence;

public sealed class OrganizationRepository(LegariaDbContext dbContext) : IOrganizationRepository
{
    public async Task<(IReadOnlyCollection<OrganizationQueryItem> Items, int Total)> ListAsync(
        int skip,
        int take,
        string? search,
        OrganizationStatus? status,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Organizations.AsNoTracking();
        if (status is not null)
        {
            query = query.Where(item => item.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{EscapeLike(search)}%";
            query = query.Where(item =>
                EF.Functions.ILike(item.TradeName, pattern, "\\") ||
                EF.Functions.ILike(item.LegalName, pattern, "\\") ||
                EF.Functions.ILike(item.Nit, pattern, "\\"));
        }

        var total = await query.CountAsync(cancellationToken);
        var organizations = await query
            .OrderBy(item => item.TradeName)
            .ThenBy(item => item.Id)
            .Skip(skip)
            .Take(take)
            .ToArrayAsync(cancellationToken);
        var results = new List<OrganizationQueryItem>(organizations.Length);
        foreach (var organization in organizations)
        {
            results.Add(await LoadDetailsAsync(organization, true, cancellationToken));
        }

        return (results, total);
    }

    public async Task<OrganizationQueryItem?> FindDetailsAsync(Guid id, CancellationToken cancellationToken)
    {
        var organization = await dbContext.Organizations
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        return organization is null
            ? null
            : await LoadDetailsAsync(organization, true, cancellationToken);
    }

    public Task<Organization?> FindOrganizationAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Organizations.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);

    public Task<UserAccount?> FindInitialAdminAsync(Guid organizationId, CancellationToken cancellationToken) =>
        dbContext.UserAccounts.SingleOrDefaultAsync(
            item => item.OrganizationId == organizationId && item.IsInitialAdministrator,
            cancellationToken);

    public Task<UserAccount?> FindUserAccountAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.UserAccounts.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);

    public Task<AccountEmail?> FindAccountEmailForUserAsync(Guid userAccountId, CancellationToken cancellationToken) =>
        dbContext.AccountEmails.SingleOrDefaultAsync(
            item => item.UserAccountId == userAccountId,
            cancellationToken);

    public Task<AccountToken?> FindLatestInvitationAsync(Guid userAccountId, CancellationToken cancellationToken) =>
        dbContext.AccountTokens
            .Where(item =>
                item.UserAccountId == userAccountId &&
                item.Purpose == AccountTokenPurpose.TenantInvitation &&
                item.RevokedAt == null)
            .OrderByDescending(item => item.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<Municipality?> FindMunicipalityAsync(string code, CancellationToken cancellationToken) =>
        dbContext.Municipalities.AsNoTracking().SingleOrDefaultAsync(item => item.Code == code, cancellationToken);

    public Task<bool> NitExistsAsync(
        string nit,
        Guid? excludingOrganizationId,
        CancellationToken cancellationToken) =>
        dbContext.Organizations.AnyAsync(
            item => item.Nit == nit &&
                (excludingOrganizationId == null || item.Id != excludingOrganizationId),
            cancellationToken);

    public Task<bool> EmailExistsAsync(
        string normalizedEmail,
        Guid? excludingUserAccountId,
        CancellationToken cancellationToken) =>
        dbContext.AccountEmails.AnyAsync(
            item => item.NormalizedEmail == normalizedEmail &&
                (excludingUserAccountId == null || item.UserAccountId != excludingUserAccountId),
            cancellationToken);

    public async Task<IReadOnlyCollection<Department>> GetDepartmentsAsync(CancellationToken cancellationToken) =>
        await dbContext.Departments
            .AsNoTracking()
            .OrderBy(item => item.Name)
            .ToArrayAsync(cancellationToken);

    public async Task<IReadOnlyCollection<Municipality>> GetMunicipalitiesAsync(
        string departmentCode,
        CancellationToken cancellationToken) =>
        await dbContext.Municipalities
            .AsNoTracking()
            .Where(item => item.DepartmentCode == departmentCode)
            .OrderBy(item => item.Name)
            .ToArrayAsync(cancellationToken);

    public void AddOrganization(Organization organization) => dbContext.Organizations.Add(organization);
    public void AddUserAccount(UserAccount account) => dbContext.UserAccounts.Add(account);
    public void AddAccountEmail(AccountEmail accountEmail) => dbContext.AccountEmails.Add(accountEmail);
    public void RemoveAccountEmail(AccountEmail accountEmail) => dbContext.AccountEmails.Remove(accountEmail);
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
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException postgres && postgres.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            if (postgres.ConstraintName is "pk_account_emails" or "ix_platform_users_normalized_email" or "ix_user_accounts_normalized_email")
            {
                throw new OrganizationException(
                    OrganizationErrorCodes.DuplicateAccountEmail,
                    "El correo ya pertenece a otra cuenta.",
                    OrganizationErrorKind.Conflict);
            }

            if (postgres.ConstraintName == "ix_organizations_nit")
            {
                throw new OrganizationException(
                    OrganizationErrorCodes.DuplicateNit,
                    "Ya existe una organización con ese NIT.",
                    OrganizationErrorKind.Conflict);
            }

            throw;
        }
    }

    private async Task<OrganizationQueryItem> LoadDetailsAsync(
        Organization organization,
        bool noTracking,
        CancellationToken cancellationToken)
    {
        var municipalityQuery = dbContext.Municipalities.Include(item => item.Department).AsQueryable();
        var adminQuery = dbContext.UserAccounts.AsQueryable();
        var invitationQuery = dbContext.AccountTokens.AsQueryable();
        if (noTracking)
        {
            municipalityQuery = municipalityQuery.AsNoTracking();
            adminQuery = adminQuery.AsNoTracking();
            invitationQuery = invitationQuery.AsNoTracking();
        }

        var municipality = await municipalityQuery.SingleAsync(
            item => item.Code == organization.MunicipalityCode,
            cancellationToken);
        var admin = await adminQuery.SingleAsync(
            item => item.OrganizationId == organization.Id && item.IsInitialAdministrator,
            cancellationToken);
        var invitation = await invitationQuery
            .Where(item =>
                item.UserAccountId == admin.Id &&
                item.Purpose == AccountTokenPurpose.TenantInvitation &&
                item.RevokedAt == null)
            .OrderByDescending(item => item.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
        var hasBranches = await dbContext.Branches
            .AsNoTracking()
            .AnyAsync(item => item.OrganizationId == organization.Id, cancellationToken);
        return new OrganizationQueryItem(
            organization,
            municipality,
            municipality.Department,
            admin,
            invitation,
            hasBranches);
    }

    private static string EscapeLike(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);
}
