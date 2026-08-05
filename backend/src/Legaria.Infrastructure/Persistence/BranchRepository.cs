using Legaria.Application.Branches;
using Legaria.Domain.Authentication;
using Legaria.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Legaria.Infrastructure.Persistence;

public sealed class BranchRepository(LegariaDbContext dbContext) : IBranchRepository
{
    public async Task<(IReadOnlyCollection<BranchQueryItem> Items, int Total)> ListBranchesAsync(
        Guid organizationId,
        Guid? assignedUserAccountId,
        int skip,
        int take,
        string? search,
        BranchStatus? status,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Branches
            .AsNoTracking()
            .Where(item => item.OrganizationId == organizationId);
        if (assignedUserAccountId is { } accountId)
        {
            query = query.Where(branch => dbContext.UserBranchAccesses.Any(access =>
                access.OrganizationId == organizationId &&
                access.UserAccountId == accountId &&
                access.BranchId == branch.Id &&
                access.RevokedAt == null));
        }

        if (status is not null)
        {
            query = query.Where(item => item.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{EscapeLike(search)}%";
            query = query.Where(item =>
                EF.Functions.ILike(item.Name, pattern, "\\") ||
                EF.Functions.ILike(item.Address, pattern, "\\"));
        }

        var total = await query.CountAsync(cancellationToken);
        var branches = await query
            .OrderBy(item => item.Name)
            .ThenBy(item => item.Id)
            .Skip(skip)
            .Take(take)
            .ToArrayAsync(cancellationToken);
        return (await LoadBranchDetailsAsync(branches, cancellationToken), total);
    }

    public async Task<BranchQueryItem?> FindBranchDetailsAsync(
        Guid organizationId,
        Guid branchId,
        Guid? assignedUserAccountId,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Branches.AsNoTracking().Where(item =>
            item.OrganizationId == organizationId && item.Id == branchId);
        if (assignedUserAccountId is { } accountId)
        {
            query = query.Where(branch => dbContext.UserBranchAccesses.Any(access =>
                access.OrganizationId == organizationId &&
                access.UserAccountId == accountId &&
                access.BranchId == branch.Id &&
                access.RevokedAt == null));
        }

        var branch = await query.SingleOrDefaultAsync(cancellationToken);
        return branch is null
            ? null
            : (await LoadBranchDetailsAsync([branch], cancellationToken)).Single();
    }

    public Task<Branch?> FindBranchAsync(
        Guid organizationId,
        Guid branchId,
        CancellationToken cancellationToken) =>
        dbContext.Branches.SingleOrDefaultAsync(
            item => item.OrganizationId == organizationId && item.Id == branchId,
            cancellationToken);

    public Task<Municipality?> FindMunicipalityAsync(string code, CancellationToken cancellationToken) =>
        dbContext.Municipalities.AsNoTracking().SingleOrDefaultAsync(item => item.Code == code, cancellationToken);

    public Task<bool> BranchNameExistsAsync(
        Guid organizationId,
        string normalizedName,
        Guid? excludingBranchId,
        CancellationToken cancellationToken) =>
        dbContext.Branches.AnyAsync(item =>
            item.OrganizationId == organizationId &&
            item.NormalizedName == normalizedName &&
            (excludingBranchId == null || item.Id != excludingBranchId),
            cancellationToken);

    public async Task<IReadOnlyCollection<Branch>> FindActiveBranchesAsync(
        Guid organizationId,
        IReadOnlyCollection<Guid> branchIds,
        CancellationToken cancellationToken) =>
        await dbContext.Branches
            .Where(item =>
                item.OrganizationId == organizationId &&
                branchIds.Contains(item.Id) &&
                item.Status == BranchStatus.Active)
            .ToArrayAsync(cancellationToken);

    public async Task<(IReadOnlyCollection<BranchAdministratorQueryItem> Items, int Total)> ListAdministratorsAsync(
        Guid organizationId,
        int skip,
        int take,
        string? search,
        AccountStatus? status,
        CancellationToken cancellationToken)
    {
        var query = dbContext.UserAccounts
            .AsNoTracking()
            .Where(item =>
                item.OrganizationId == organizationId &&
                item.Roles.Any(role => role.SystemRoleId == SystemRole.BranchAdminId));
        if (status is not null)
        {
            query = query.Where(item => item.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{EscapeLike(search)}%";
            query = query.Where(item =>
                EF.Functions.ILike(item.FirstName, pattern, "\\") ||
                EF.Functions.ILike(item.LastName, pattern, "\\") ||
                EF.Functions.ILike(item.Email, pattern, "\\"));
        }

        var total = await query.CountAsync(cancellationToken);
        var accounts = await query
            .OrderBy(item => item.FirstName)
            .ThenBy(item => item.LastName)
            .ThenBy(item => item.Id)
            .Skip(skip)
            .Take(take)
            .ToArrayAsync(cancellationToken);
        return (await LoadAdministratorDetailsAsync(accounts, cancellationToken), total);
    }

    public async Task<BranchAdministratorQueryItem?> FindAdministratorDetailsAsync(
        Guid organizationId,
        Guid accountId,
        CancellationToken cancellationToken)
    {
        var account = await dbContext.UserAccounts
            .AsNoTracking()
            .SingleOrDefaultAsync(item =>
                item.OrganizationId == organizationId &&
                item.Id == accountId &&
                item.Roles.Any(role => role.SystemRoleId == SystemRole.BranchAdminId),
                cancellationToken);
        return account is null
            ? null
            : (await LoadAdministratorDetailsAsync([account], cancellationToken)).Single();
    }

    public Task<UserAccount?> FindAdministratorAsync(
        Guid organizationId,
        Guid accountId,
        CancellationToken cancellationToken) =>
        dbContext.UserAccounts.SingleOrDefaultAsync(item =>
            item.OrganizationId == organizationId &&
            item.Id == accountId &&
            item.Roles.Any(role => role.SystemRoleId == SystemRole.BranchAdminId),
            cancellationToken);

    public Task<AccountEmail?> FindAccountEmailAsync(Guid accountId, CancellationToken cancellationToken) =>
        dbContext.AccountEmails.SingleOrDefaultAsync(item => item.UserAccountId == accountId, cancellationToken);

    public Task<bool> EmailExistsAsync(
        string normalizedEmail,
        Guid? excludingAccountId,
        CancellationToken cancellationToken) =>
        dbContext.AccountEmails.AnyAsync(item =>
            item.NormalizedEmail == normalizedEmail &&
            (excludingAccountId == null || item.UserAccountId != excludingAccountId),
            cancellationToken);

    public async Task<IReadOnlyCollection<UserBranchAccess>> FindActiveAccessesAsync(
        Guid organizationId,
        Guid accountId,
        CancellationToken cancellationToken) =>
        await dbContext.UserBranchAccesses
            .Where(item =>
                item.OrganizationId == organizationId &&
                item.UserAccountId == accountId &&
                item.RevokedAt == null)
            .ToArrayAsync(cancellationToken);

    public async Task<IReadOnlyCollection<RefreshSession>> FindActiveSessionsAsync(
        Guid accountId,
        DateTimeOffset now,
        CancellationToken cancellationToken) =>
        await dbContext.RefreshSessions
            .Where(item => item.UserAccountId == accountId && item.RevokedAt == null && item.ExpiresAt > now)
            .ToArrayAsync(cancellationToken);

    public async Task<IReadOnlyCollection<AccountToken>> FindActiveInvitationsAsync(
        Guid accountId,
        CancellationToken cancellationToken) =>
        await dbContext.AccountTokens
            .Where(item =>
                item.UserAccountId == accountId &&
                item.Purpose == AccountTokenPurpose.TenantInvitation &&
                item.UsedAt == null &&
                item.RevokedAt == null)
            .ToArrayAsync(cancellationToken);

    public Task<Organization?> FindOrganizationAsync(Guid organizationId, CancellationToken cancellationToken) =>
        dbContext.Organizations.SingleOrDefaultAsync(item => item.Id == organizationId, cancellationToken);

    public void AddBranch(Branch branch) => dbContext.Branches.Add(branch);
    public void AddUserAccount(UserAccount account) => dbContext.UserAccounts.Add(account);
    public void AddAccountEmail(AccountEmail accountEmail) => dbContext.AccountEmails.Add(accountEmail);
    public void RemoveAccountEmail(AccountEmail accountEmail) => dbContext.AccountEmails.Remove(accountEmail);
    public void AddAccess(UserBranchAccess access) => dbContext.UserBranchAccesses.Add(access);
    public void AddAuditEvent(SecurityAuditEvent auditEvent) => dbContext.SecurityAuditEvents.Add(auditEvent);

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException postgres && postgres.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            if (postgres.ConstraintName == "ix_branches_organization_id_normalized_name")
            {
                throw new BranchException(
                    BranchErrorCodes.DuplicateName,
                    "Ya existe una sucursal con ese nombre en la organización.",
                    BranchErrorKind.Conflict);
            }

            if (postgres.ConstraintName is "pk_account_emails" or "ix_user_accounts_normalized_email")
            {
                throw new BranchException(
                    BranchErrorCodes.DuplicateAccountEmail,
                    "El correo ya pertenece a otra cuenta.",
                    BranchErrorKind.Conflict);
            }

            throw;
        }
    }

    private async Task<IReadOnlyCollection<BranchQueryItem>> LoadBranchDetailsAsync(
        IReadOnlyCollection<Branch> branches,
        CancellationToken cancellationToken)
    {
        if (branches.Count == 0)
        {
            return [];
        }

        var codes = branches.Select(item => item.MunicipalityCode).Distinct().ToArray();
        var municipalities = await dbContext.Municipalities
            .AsNoTracking()
            .Include(item => item.Department)
            .Where(item => codes.Contains(item.Code))
            .ToDictionaryAsync(item => item.Code, cancellationToken);
        return branches
            .Select(branch => new BranchQueryItem(
                branch,
                municipalities[branch.MunicipalityCode],
                municipalities[branch.MunicipalityCode].Department))
            .ToArray();
    }

    private async Task<IReadOnlyCollection<BranchAdministratorQueryItem>> LoadAdministratorDetailsAsync(
        IReadOnlyCollection<UserAccount> accounts,
        CancellationToken cancellationToken)
    {
        if (accounts.Count == 0)
        {
            return [];
        }

        var accountIds = accounts.Select(item => item.Id).ToArray();
        var invitations = await dbContext.AccountTokens
            .AsNoTracking()
            .Where(item =>
                item.UserAccountId != null &&
                accountIds.Contains(item.UserAccountId.Value) &&
                item.Purpose == AccountTokenPurpose.TenantInvitation)
            .OrderByDescending(item => item.CreatedAt)
            .ToArrayAsync(cancellationToken);
        var invitationByAccount = invitations
            .GroupBy(item => item.UserAccountId!.Value)
            .ToDictionary(group => group.Key, group => group.First());
        var accesses = await dbContext.UserBranchAccesses
            .AsNoTracking()
            .Where(item => accountIds.Contains(item.UserAccountId) && item.RevokedAt == null)
            .ToArrayAsync(cancellationToken);
        var branchIds = accesses.Select(item => item.BranchId).Distinct().ToArray();
        var branches = await dbContext.Branches
            .AsNoTracking()
            .Where(item => branchIds.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, cancellationToken);
        var accessByAccount = accesses
            .GroupBy(item => item.UserAccountId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyCollection<Branch>)group
                    .Select(access => branches[access.BranchId])
                    .OrderBy(branch => branch.Name)
                    .ToArray());

        return accounts.Select(account => new BranchAdministratorQueryItem(
            account,
            invitationByAccount.GetValueOrDefault(account.Id),
            accessByAccount.GetValueOrDefault(account.Id) ?? [])).ToArray();
    }

    private static string EscapeLike(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);
}
