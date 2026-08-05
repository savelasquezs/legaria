using Legaria.Application.Employees;
using Legaria.Domain.Authentication;
using Legaria.Domain.Documents;
using Legaria.Domain.Employees;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace Legaria.Infrastructure.Persistence;

public sealed class EmployeeRepository(LegariaDbContext dbContext) : IEmployeeRepository
{
    public async Task<(IReadOnlyCollection<EmployeeQueryItem> Items, int Total)> ListAsync(
        Guid organizationId,
        Guid? branchId,
        Guid? excludeBranchId,
        Guid? excludedEmployeeId,
        int skip,
        int take,
        string? search,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Employees.AsNoTracking().Where(item => item.OrganizationId == organizationId);
        if (excludedEmployeeId is { } excludedEmployee)
        {
            query = query.Where(item => item.Id != excludedEmployee);
        }
        if (branchId is { } includedBranch)
        {
            query = query.Where(employee =>
                dbContext.EmploymentRelationships.Any(relationship =>
                    relationship.OrganizationId == organizationId &&
                    relationship.EmployeeId == employee.Id &&
                    dbContext.EmployeeAssignments.Any(assignment =>
                        assignment.OrganizationId == organizationId &&
                        assignment.EmploymentRelationshipId == relationship.Id &&
                        assignment.BranchId == includedBranch &&
                        assignment.EndedOn == null)));
        }

        if (excludeBranchId is { } excludedBranch)
        {
            query = query.Where(employee =>
                !dbContext.EmploymentRelationships.Any(relationship =>
                    relationship.OrganizationId == organizationId &&
                    relationship.EmployeeId == employee.Id &&
                    dbContext.EmployeeAssignments.Any(assignment =>
                        assignment.OrganizationId == organizationId &&
                        assignment.EmploymentRelationshipId == relationship.Id &&
                        assignment.BranchId == excludedBranch &&
                        assignment.EndedOn == null)));
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{EscapeLike(search)}%";
            query = query.Where(item =>
                EF.Functions.ILike(item.FirstName, pattern, "\\") ||
                EF.Functions.ILike(item.LastName, pattern, "\\") ||
                EF.Functions.ILike(item.DocumentNumber, pattern, "\\"));
        }

        var total = await query.CountAsync(cancellationToken);
        var employees = await query
            .OrderBy(item => item.FirstName)
            .ThenBy(item => item.LastName)
            .ThenBy(item => item.Id)
            .Skip(skip)
            .Take(take)
            .ToArrayAsync(cancellationToken);
        return (await LoadDetailsAsync(employees, cancellationToken), total);
    }

    public async Task<EmployeeQueryItem?> FindDetailsAsync(
        Guid organizationId,
        Guid employeeId,
        CancellationToken cancellationToken)
    {
        var employee = await dbContext.Employees
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.OrganizationId == organizationId && item.Id == employeeId, cancellationToken);
        return employee is null ? null : (await LoadDetailsAsync([employee], cancellationToken)).Single();
    }

    public async Task<EmployeeDetailQueryItem?> FindEmploymentDetailsAsync(
        Guid organizationId,
        Guid employeeId,
        CancellationToken cancellationToken)
    {
        var employee = await dbContext.Employees.AsNoTracking().SingleOrDefaultAsync(
            item => item.OrganizationId == organizationId && item.Id == employeeId,
            cancellationToken);
        if (employee is null)
        {
            return null;
        }

        var relationships = await dbContext.EmploymentRelationships.AsNoTracking()
            .Where(item => item.OrganizationId == organizationId && item.EmployeeId == employeeId)
            .OrderByDescending(item => item.StartedOn)
            .ToArrayAsync(cancellationToken);
        var relationshipIds = relationships.Select(item => item.Id).ToArray();
        var assignments = await dbContext.EmployeeAssignments.AsNoTracking()
            .Where(item => item.OrganizationId == organizationId && relationshipIds.Contains(item.EmploymentRelationshipId))
            .ToArrayAsync(cancellationToken);
        var branchIds = assignments.Select(item => item.BranchId).Distinct().ToArray();
        var positionIds = assignments.Select(item => item.JobPositionId).Distinct().ToArray();
        var branches = await dbContext.Branches.AsNoTracking()
            .Where(item => item.OrganizationId == organizationId && branchIds.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, cancellationToken);
        var positions = await dbContext.JobPositions.AsNoTracking()
            .Where(item => item.OrganizationId == organizationId && positionIds.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, cancellationToken);
        var account = await dbContext.UserAccounts.AsNoTracking()
            .SingleOrDefaultAsync(item => item.OrganizationId == organizationId && item.EmployeeId == employeeId, cancellationToken);
        AccountToken? invitation = null;
        IReadOnlyCollection<Guid> administrativeBranchIds = [];
        if (account is not null)
        {
            invitation = await dbContext.AccountTokens.AsNoTracking()
                .Where(item => item.UserAccountId == account.Id && item.Purpose == AccountTokenPurpose.TenantInvitation)
                .OrderByDescending(item => item.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);
            administrativeBranchIds = await dbContext.UserBranchAccesses.AsNoTracking()
                .Where(item => item.OrganizationId == organizationId && item.UserAccountId == account.Id && item.RevokedAt == null)
                .Select(item => item.BranchId)
                .ToArrayAsync(cancellationToken);
        }

        return new EmployeeDetailQueryItem(
            employee,
            relationships.Select(relationship => new EmploymentRelationshipQueryItem(
                relationship,
                assignments.Where(item => item.EmploymentRelationshipId == relationship.Id)
                    .Select(item => new EmployeeAssignmentQueryItem(item, branches[item.BranchId], positions[item.JobPositionId]))
                    .OrderByDescending(item => item.Assignment.StartedOn)
                    .ToArray())).ToArray(),
            account,
            invitation,
            administrativeBranchIds);
    }

    public Task<Employee?> FindAsync(Guid organizationId, Guid employeeId, CancellationToken cancellationToken) =>
        dbContext.Employees.SingleOrDefaultAsync(
            item => item.OrganizationId == organizationId && item.Id == employeeId,
            cancellationToken);

    public Task<bool> DocumentExistsAsync(
        Guid organizationId,
        string documentType,
        string documentNumber,
        CancellationToken cancellationToken) =>
        dbContext.Employees.AnyAsync(item =>
            item.OrganizationId == organizationId &&
            item.DocumentType == documentType &&
            item.DocumentNumber == documentNumber,
            cancellationToken);

    public Task<EmploymentRelationship?> FindActiveRelationshipAsync(
        Guid organizationId,
        Guid employeeId,
        CancellationToken cancellationToken) =>
        dbContext.EmploymentRelationships
            .OrderByDescending(item => item.StartedOn)
            .FirstOrDefaultAsync(item =>
                item.OrganizationId == organizationId &&
                item.EmployeeId == employeeId &&
                item.EndedOn == null,
                cancellationToken);

    public Task<EmploymentRelationship?> FindLatestRelationshipAsync(
        Guid organizationId,
        Guid employeeId,
        CancellationToken cancellationToken) =>
        dbContext.EmploymentRelationships
            .OrderByDescending(item => item.StartedOn)
            .FirstOrDefaultAsync(item => item.OrganizationId == organizationId && item.EmployeeId == employeeId, cancellationToken);

    public Task<EmploymentRelationship?> FindRelationshipAsync(
        Guid organizationId,
        Guid employeeId,
        Guid relationshipId,
        CancellationToken cancellationToken) =>
        dbContext.EmploymentRelationships.SingleOrDefaultAsync(item =>
            item.OrganizationId == organizationId &&
            item.EmployeeId == employeeId &&
            item.Id == relationshipId,
            cancellationToken);

    public async Task<IReadOnlyCollection<EmployeeAssignment>> FindActiveAssignmentsAsync(
        Guid organizationId,
        Guid relationshipId,
        CancellationToken cancellationToken) =>
        await dbContext.EmployeeAssignments.Where(item =>
            item.OrganizationId == organizationId &&
            item.EmploymentRelationshipId == relationshipId &&
            item.EndedOn == null).ToArrayAsync(cancellationToken);

    public Task<EmployeeAssignment?> FindAssignmentAsync(
        Guid organizationId,
        Guid employeeId,
        Guid assignmentId,
        CancellationToken cancellationToken) =>
        dbContext.EmployeeAssignments.SingleOrDefaultAsync(assignment =>
            assignment.OrganizationId == organizationId &&
            assignment.Id == assignmentId &&
            dbContext.EmploymentRelationships.Any(relationship =>
                relationship.OrganizationId == organizationId &&
                relationship.Id == assignment.EmploymentRelationshipId &&
                relationship.EmployeeId == employeeId),
            cancellationToken);

    public Task<bool> ActiveAssignmentExistsAsync(
        Guid organizationId,
        Guid relationshipId,
        Guid branchId,
        CancellationToken cancellationToken) =>
        dbContext.EmployeeAssignments.AnyAsync(item =>
            item.OrganizationId == organizationId &&
            item.EmploymentRelationshipId == relationshipId &&
            item.BranchId == branchId &&
            item.EndedOn == null,
            cancellationToken);

    public Task<bool> ActivePrimaryAssignmentExistsAsync(
        Guid organizationId,
        Guid relationshipId,
        CancellationToken cancellationToken) =>
        dbContext.EmployeeAssignments.AnyAsync(item =>
            item.OrganizationId == organizationId &&
            item.EmploymentRelationshipId == relationshipId &&
            item.IsPrimary &&
            item.EndedOn == null,
            cancellationToken);

    public Task<bool> AssignmentPeriodOverlapsAsync(
        Guid organizationId,
        Guid relationshipId,
        Guid branchId,
        DateOnly startedOn,
        DateOnly? endedOn,
        Guid? excludingAssignmentId,
        CancellationToken cancellationToken)
    {
        var upperBound = endedOn ?? DateOnly.MaxValue;
        return dbContext.EmployeeAssignments.AnyAsync(item =>
            item.OrganizationId == organizationId &&
            item.EmploymentRelationshipId == relationshipId &&
            item.BranchId == branchId &&
            item.Id != excludingAssignmentId &&
            item.StartedOn <= upperBound &&
            (item.EndedOn == null || item.EndedOn >= startedOn),
            cancellationToken);
    }

    public Task<JobPosition?> FindActiveJobPositionAsync(Guid organizationId, Guid id, CancellationToken cancellationToken) =>
        dbContext.JobPositions.SingleOrDefaultAsync(item =>
            item.OrganizationId == organizationId &&
            item.Id == id &&
            item.Status == JobPositionStatus.Active,
            cancellationToken);

    public Task<JobPosition?> FindJobPositionAsync(Guid organizationId, Guid id, CancellationToken cancellationToken) =>
        dbContext.JobPositions.SingleOrDefaultAsync(item => item.OrganizationId == organizationId && item.Id == id, cancellationToken);

    public async Task<IReadOnlyCollection<JobPositionQueryItem>> ListJobPositionsAsync(
        Guid organizationId,
        JobPositionStatus? status,
        CancellationToken cancellationToken) =>
        await dbContext.JobPositions
            .AsNoTracking()
            .Where(item => item.OrganizationId == organizationId && (status == null || item.Status == status))
            .OrderBy(item => item.Name)
            .Select(item => new JobPositionQueryItem(
                item,
                dbContext.JobPositionDocumentRequirements.Count(requirement =>
                    requirement.OrganizationId == organizationId &&
                    requirement.JobPositionId == item.Id)))
            .ToArrayAsync(cancellationToken);

    public Task<bool> JobPositionNameExistsAsync(
        Guid organizationId,
        string normalizedName,
        Guid? excludingId,
        CancellationToken cancellationToken) =>
        dbContext.JobPositions.AnyAsync(item =>
            item.OrganizationId == organizationId && item.NormalizedName == normalizedName && item.Id != excludingId,
            cancellationToken);

    public async Task<IReadOnlyCollection<Guid>> ListJobPositionDocumentRequirementIdsAsync(
        Guid organizationId,
        Guid jobPositionId,
        CancellationToken cancellationToken) =>
        await dbContext.JobPositionDocumentRequirements
            .AsNoTracking()
            .Where(item => item.OrganizationId == organizationId && item.JobPositionId == jobPositionId)
            .OrderBy(item => item.DocumentTypeId)
            .Select(item => item.DocumentTypeId)
            .ToArrayAsync(cancellationToken);

    public Task<int> CountJobPositionDocumentRequirementsAsync(
        Guid organizationId,
        Guid jobPositionId,
        CancellationToken cancellationToken) =>
        dbContext.JobPositionDocumentRequirements.CountAsync(item =>
            item.OrganizationId == organizationId && item.JobPositionId == jobPositionId,
            cancellationToken);

    public async Task<bool> AreAvailableEmployeeDocumentTypesAsync(
        Guid organizationId,
        IReadOnlyCollection<Guid> documentTypeIds,
        CancellationToken cancellationToken)
    {
        if (documentTypeIds.Count == 0)
        {
            return true;
        }

        var availableCount = await (
            from documentType in dbContext.DocumentTypes.AsNoTracking()
            join category in dbContext.DocumentCategories.AsNoTracking()
                on new { documentType.OrganizationId, Id = documentType.CategoryId }
                equals new { category.OrganizationId, category.Id }
            where documentType.OrganizationId == organizationId &&
                documentTypeIds.Contains(documentType.Id) &&
                documentType.Status == DocumentCatalogStatus.Active &&
                category.Status == DocumentCatalogStatus.Active &&
                category.Scope == DocumentScope.Employee
            select documentType.Id)
            .CountAsync(cancellationToken);
        return availableCount == documentTypeIds.Count;
    }

    public async Task ReplaceJobPositionDocumentRequirementsAsync(
        Guid organizationId,
        Guid jobPositionId,
        IReadOnlyCollection<Guid> documentTypeIds,
        CancellationToken cancellationToken)
    {
        var current = await dbContext.JobPositionDocumentRequirements
            .Where(item => item.OrganizationId == organizationId && item.JobPositionId == jobPositionId)
            .ToArrayAsync(cancellationToken);
        var selectedIds = documentTypeIds.ToHashSet();
        var currentIds = current.Select(item => item.DocumentTypeId).ToHashSet();
        dbContext.JobPositionDocumentRequirements.RemoveRange(
            current.Where(item => !selectedIds.Contains(item.DocumentTypeId)));
        dbContext.JobPositionDocumentRequirements.AddRange(
            selectedIds.Where(documentTypeId => !currentIds.Contains(documentTypeId)).Select(documentTypeId =>
            JobPositionDocumentRequirement.Create(organizationId, jobPositionId, documentTypeId)));
    }

    public Task<UserAccount?> FindLinkedAccountAsync(
        Guid organizationId,
        Guid employeeId,
        CancellationToken cancellationToken) =>
        dbContext.UserAccounts
            .Include(item => item.Roles)
            .ThenInclude(item => item.SystemRole)
            .SingleOrDefaultAsync(item =>
                item.OrganizationId == organizationId && item.EmployeeId == employeeId,
                cancellationToken);

    public void AddEmployee(Employee employee) => dbContext.Employees.Add(employee);
    public void AddRelationship(EmploymentRelationship relationship) => dbContext.EmploymentRelationships.Add(relationship);
    public void AddAssignment(EmployeeAssignment assignment) => dbContext.EmployeeAssignments.Add(assignment);
    public void AddJobPosition(JobPosition position) => dbContext.JobPositions.Add(position);

    public async Task<IEmployeeTransaction> BeginTransactionAsync(CancellationToken cancellationToken) =>
        new EmployeeTransaction(await dbContext.Database.BeginTransactionAsync(cancellationToken));

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException postgres && postgres.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            if (postgres.ConstraintName == "ix_employees_organization_id_document_type_document_number")
            {
                throw new EmployeeException(
                    EmployeeErrorCodes.DuplicateDocument,
                    "Ya existe un trabajador con ese tipo y número de documento.",
                    EmployeeErrorKind.Conflict);
            }

            if (postgres.ConstraintName == "ix_job_positions_organization_id_normalized_name")
            {
                throw new EmployeeException(
                    EmployeeErrorCodes.JobPositionDuplicateName,
                    "Ya existe un cargo con ese nombre.",
                    EmployeeErrorKind.Conflict);
            }

            if (postgres.ConstraintName is "ix_employment_relationships_active_employee" or "ix_employee_assignments_active_branch")
            {
                throw new EmployeeException(
                    postgres.ConstraintName == "ix_employment_relationships_active_employee"
                        ? EmployeeErrorCodes.RelationshipInvalidState
                        : EmployeeErrorCodes.DuplicateAssignment,
                    postgres.ConstraintName == "ix_employment_relationships_active_employee"
                        ? "El trabajador ya tiene una relación laboral activa."
                        : "El trabajador ya tiene una asignación activa en esta sucursal.",
                    EmployeeErrorKind.Conflict);
            }

            if (postgres.ConstraintName is "pk_account_emails" or "ix_user_accounts_organization_id_employee_id")
            {
                throw new EmployeeException(
                    postgres.ConstraintName == "pk_account_emails" ? EmployeeErrorCodes.DuplicateEmail : EmployeeErrorCodes.DuplicateAccount,
                    postgres.ConstraintName == "pk_account_emails" ? "El correo ya pertenece a otra cuenta." : "El trabajador ya tiene una cuenta de acceso.",
                    EmployeeErrorKind.Conflict);
            }

            throw;
        }
    }

    private async Task<IReadOnlyCollection<EmployeeQueryItem>> LoadDetailsAsync(
        IReadOnlyCollection<Employee> employees,
        CancellationToken cancellationToken)
    {
        if (employees.Count == 0)
        {
            return [];
        }

        var employeeIds = employees.Select(item => item.Id).ToArray();
        var relationships = await dbContext.EmploymentRelationships
            .AsNoTracking()
            .Where(item => item.EndedOn == null && employeeIds.Contains(item.EmployeeId))
            .ToArrayAsync(cancellationToken);
        var relationshipIds = relationships.Select(item => item.Id).ToArray();
        var assignments = await dbContext.EmployeeAssignments
            .AsNoTracking()
            .Where(item => item.EndedOn == null && relationshipIds.Contains(item.EmploymentRelationshipId))
            .ToArrayAsync(cancellationToken);
        var branchIds = assignments.Select(item => item.BranchId).Distinct().ToArray();
        var positionIds = assignments.Select(item => item.JobPositionId).Distinct().ToArray();
        var branches = await dbContext.Branches.AsNoTracking()
            .Where(item => branchIds.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, cancellationToken);
        var positions = await dbContext.JobPositions.AsNoTracking()
            .Where(item => positionIds.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, cancellationToken);
        var relationshipEmployee = relationships.ToDictionary(item => item.Id, item => item.EmployeeId);
        var assignmentsByEmployee = assignments
            .GroupBy(item => relationshipEmployee[item.EmploymentRelationshipId])
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyCollection<EmployeeAssignmentQueryItem>)group
                    .Select(item => new EmployeeAssignmentQueryItem(item, branches[item.BranchId], positions[item.JobPositionId]))
                    .OrderByDescending(item => item.Assignment.IsPrimary)
                    .ThenBy(item => item.Branch.Name)
                    .ToArray());
        var accounts = await dbContext.UserAccounts
            .AsNoTracking()
            .Include(item => item.Roles)
            .ThenInclude(item => item.SystemRole)
            .Where(item => item.EmployeeId != null && employeeIds.Contains(item.EmployeeId.Value))
            .ToArrayAsync(cancellationToken);
        var accountByEmployee = accounts.ToDictionary(item => item.EmployeeId!.Value);
        var accountIds = accounts.Select(item => item.Id).ToArray();
        var invitations = await dbContext.AccountTokens.AsNoTracking()
            .Where(item => item.UserAccountId != null && accountIds.Contains(item.UserAccountId.Value) && item.Purpose == AccountTokenPurpose.TenantInvitation)
            .OrderByDescending(item => item.CreatedAt)
            .ToArrayAsync(cancellationToken);
        var invitationByAccount = invitations.GroupBy(item => item.UserAccountId!.Value).ToDictionary(group => group.Key, group => group.First());
        var accesses = await dbContext.UserBranchAccesses.AsNoTracking()
            .Where(item => accountIds.Contains(item.UserAccountId) && item.RevokedAt == null)
            .ToArrayAsync(cancellationToken);
        var accessesByAccount = accesses.GroupBy(item => item.UserAccountId).ToDictionary(group => group.Key, group => (IReadOnlyCollection<Guid>)group.Select(item => item.BranchId).ToArray());

        return employees.Select(employee =>
        {
            accountByEmployee.TryGetValue(employee.Id, out var account);
            return new EmployeeQueryItem(
                employee,
                assignmentsByEmployee.GetValueOrDefault(employee.Id) ?? [],
                account,
                account is null ? null : invitationByAccount.GetValueOrDefault(account.Id),
                account is null ? [] : accessesByAccount.GetValueOrDefault(account.Id) ?? []);
        }).ToArray();
    }

    private static string EscapeLike(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);

    private sealed class EmployeeTransaction(IDbContextTransaction transaction) : IEmployeeTransaction
    {
        public Task CommitAsync(CancellationToken cancellationToken) => transaction.CommitAsync(cancellationToken);
        public ValueTask DisposeAsync() => transaction.DisposeAsync();
    }
}
