using System.Net.Mail;
using Legaria.Application.Authentication;
using Legaria.Application.Branches;
using Legaria.Domain.Authentication;
using Legaria.Domain.Employees;
using Legaria.Domain.Tenancy;

namespace Legaria.Application.Employees;

public sealed class EmployeeService(
    IEmployeeRepository repository,
    IBranchRepository branchRepository,
    IEmailNormalizer emailNormalizer,
    IPasswordService passwordService,
    ISecureTokenService secureTokenService,
    ITenantInvitationService tenantInvitations,
    IClock clock) : IEmployeeService
{
    public async Task<EmployeePage> ListAsync(
        int page,
        int pageSize,
        string? search,
        Guid? branchId,
        Guid? excludeBranchId,
        CurrentAccount actor,
        CancellationToken cancellationToken)
    {
        var organizationId = EnsureSuperAdministrator(actor);
        ValidatePagination(page, pageSize);
        if (branchId is { } included && await branchRepository.FindBranchAsync(organizationId, included, cancellationToken) is null)
        {
            throw EmployeeNotFound();
        }

        var (items, total) = await repository.ListAsync(
            organizationId,
            branchId,
            excludeBranchId,
            (page - 1) * pageSize,
            pageSize,
            CleanOptional(search, 200),
            cancellationToken);
        return new EmployeePage(
            items.Select(ToResult).ToArray(),
            page,
            pageSize,
            total,
            total == 0 ? 0 : (int)Math.Ceiling(total / (double)pageSize));
    }

    public async Task<EmployeeResult> GetAsync(Guid id, CurrentAccount actor, CancellationToken cancellationToken)
    {
        var organizationId = EnsureSuperAdministrator(actor);
        return ToResult(await repository.FindDetailsAsync(organizationId, id, cancellationToken)
            ?? throw EmployeeNotFound());
    }

    public async Task<EmployeeResult> CreateAsync(
        Guid branchId,
        CreateEmployeeInput input,
        CurrentAccount actor,
        ClientContext client,
        CancellationToken cancellationToken)
    {
        var organizationId = EnsureSuperAdministrator(actor);
        var branch = await FindActiveBranchAsync(organizationId, branchId, cancellationToken);
        var position = await FindPositionAsync(organizationId, input.JobPositionId, cancellationToken);
        var identity = ValidateIdentity(input.DocumentType, input.DocumentNumber, input.FirstName, input.LastName);
        if (await repository.DocumentExistsAsync(
            organizationId,
            identity.DocumentType,
            identity.DocumentNumber,
            cancellationToken))
        {
            throw DuplicateDocument();
        }

        var now = clock.UtcNow;
        var employee = Employee.Create(
            organizationId,
            identity.DocumentType,
            identity.DocumentNumber,
            identity.FirstName,
            identity.LastName,
            now);
        var relationship = EmploymentRelationship.Create(organizationId, employee.Id, input.StartedOn, now);
        var assignment = EmployeeAssignment.Create(
            organizationId,
            relationship.Id,
            branch.Id,
            position.Id,
            input.IsPrimary,
            input.StartedOn,
            now);
        repository.AddEmployee(employee);
        repository.AddRelationship(relationship);
        repository.AddAssignment(assignment);

        var provisioned = await ProvisionAdministrativeAccessAsync(
            employee,
            input.AdministrativeAccess,
            actor,
            client,
            cancellationToken);
        branchRepository.AddAuditEvent(CreateAudit(
            "EMPLOYEE_CREATED",
            actor,
            organizationId,
            provisioned.Account?.Id,
            client,
            now,
            branch.Id));
        await repository.SaveChangesAsync(cancellationToken);
        await DeliverIfCreatedAsync(provisioned, organizationId, actor, client, cancellationToken);
        return await GetAsync(employee.Id, actor, cancellationToken);
    }

    public async Task<EmployeeResult> AssignAsync(
        Guid branchId,
        Guid employeeId,
        AssignEmployeeInput input,
        CurrentAccount actor,
        ClientContext client,
        CancellationToken cancellationToken)
    {
        var organizationId = EnsureSuperAdministrator(actor);
        var employee = await repository.FindAsync(organizationId, employeeId, cancellationToken)
            ?? throw EmployeeNotFound();
        var branch = await FindActiveBranchAsync(organizationId, branchId, cancellationToken);
        var position = await FindPositionAsync(organizationId, input.JobPositionId, cancellationToken);
        var now = clock.UtcNow;
        var relationship = await repository.FindActiveRelationshipAsync(organizationId, employeeId, cancellationToken);
        if (relationship is null)
        {
            relationship = EmploymentRelationship.Create(organizationId, employeeId, input.StartedOn, now);
            repository.AddRelationship(relationship);
        }
        else
        {
            if (await repository.ActiveAssignmentExistsAsync(organizationId, relationship.Id, branchId, cancellationToken))
            {
                throw new EmployeeException(
                    EmployeeErrorCodes.DuplicateAssignment,
                    "El trabajador ya tiene una asignación activa en esta sucursal.",
                    EmployeeErrorKind.Conflict);
            }

            if (input.IsPrimary && await repository.ActivePrimaryAssignmentExistsAsync(organizationId, relationship.Id, cancellationToken))
            {
                throw new EmployeeException(
                    EmployeeErrorCodes.DuplicateAssignment,
                    "La relación laboral ya tiene una asignación principal activa.",
                    EmployeeErrorKind.Conflict);
            }
        }

        repository.AddAssignment(EmployeeAssignment.Create(
            organizationId,
            relationship.Id,
            branch.Id,
            position.Id,
            input.IsPrimary,
            input.StartedOn,
            now));
        var provisioned = await ProvisionAdministrativeAccessAsync(
            employee,
            input.AdministrativeAccess,
            actor,
            client,
            cancellationToken);
        branchRepository.AddAuditEvent(CreateAudit(
            "EMPLOYEE_ASSIGNED_TO_BRANCH",
            actor,
            organizationId,
            provisioned.Account?.Id,
            client,
            now,
            branch.Id));
        await repository.SaveChangesAsync(cancellationToken);
        await DeliverIfCreatedAsync(provisioned, organizationId, actor, client, cancellationToken);
        return await GetAsync(employee.Id, actor, cancellationToken);
    }

    public async Task<EmployeeResult> GrantAdministrativeAccessAsync(
        Guid employeeId,
        AdministrativeAccessInput input,
        CurrentAccount actor,
        ClientContext client,
        CancellationToken cancellationToken)
    {
        var organizationId = EnsureSuperAdministrator(actor);
        var employee = await repository.FindAsync(organizationId, employeeId, cancellationToken)
            ?? throw EmployeeNotFound();
        var provisioned = await ProvisionAdministrativeAccessAsync(
            employee,
            input,
            actor,
            client,
            cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        await DeliverIfCreatedAsync(provisioned, organizationId, actor, client, cancellationToken);
        return await GetAsync(employee.Id, actor, cancellationToken);
    }

    public async Task<IReadOnlyCollection<JobPositionResult>> ListJobPositionsAsync(
        CurrentAccount actor,
        CancellationToken cancellationToken)
    {
        var organizationId = EnsureSuperAdministrator(actor);
        return (await repository.ListActiveJobPositionsAsync(organizationId, cancellationToken))
            .Select(ToPositionResult)
            .ToArray();
    }

    public async Task<JobPositionResult> CreateJobPositionAsync(
        JobPositionInput input,
        CurrentAccount actor,
        CancellationToken cancellationToken)
    {
        var organizationId = EnsureSuperAdministrator(actor);
        var name = CleanRequired(input.Name, 150, "nombre del cargo");
        var normalizedName = name.ToUpperInvariant();
        if (await repository.JobPositionNameExistsAsync(organizationId, normalizedName, cancellationToken))
        {
            throw new EmployeeException(
                EmployeeErrorCodes.JobPositionDuplicateName,
                "Ya existe un cargo con ese nombre.",
                EmployeeErrorKind.Conflict);
        }

        var position = JobPosition.Create(organizationId, name, normalizedName, clock.UtcNow);
        repository.AddJobPosition(position);
        await repository.SaveChangesAsync(cancellationToken);
        return ToPositionResult(position);
    }

    private async Task<ProvisionedAccess> ProvisionAdministrativeAccessAsync(
        Employee employee,
        AdministrativeAccessInput? input,
        CurrentAccount actor,
        ClientContext client,
        CancellationToken cancellationToken)
    {
        if (input is null)
        {
            return new ProvisionedAccess(null, null, null);
        }

        var organizationId = actor.OrganizationId!.Value;
        var branches = await ValidateAdministrativeBranchesAsync(organizationId, input.BranchIds, cancellationToken);
        var account = await repository.FindLinkedAccountAsync(organizationId, employee.Id, cancellationToken);
        IssuedTenantInvitation? invitation = null;
        Organization? organization = null;
        var now = clock.UtcNow;
        if (account is null)
        {
            var email = ValidateEmail(input.Email);
            if (await branchRepository.EmailExistsAsync(email.Normalized, null, cancellationToken))
            {
                throw new EmployeeException(
                    EmployeeErrorCodes.DuplicateEmail,
                    "El correo ya pertenece a otra cuenta.",
                    EmployeeErrorKind.Conflict);
            }

            account = UserAccount.Create(
                organizationId,
                employee.Id,
                email.Value,
                email.Normalized,
                passwordService.Hash(secureTokenService.GenerateToken()),
                employee.FirstName,
                employee.LastName,
                secureTokenService.GenerateSecurityStamp(),
                false,
                now);
            account.AddRole(SystemRole.BranchAdminId);
            branchRepository.AddUserAccount(account);
            branchRepository.AddAccountEmail(AccountEmail.ForTenant(email.Normalized, account.Id, now));
            organization = await branchRepository.FindOrganizationAsync(organizationId, cancellationToken)
                ?? throw new EmployeeException(EmployeeErrorCodes.Forbidden, "La organización no está disponible.", EmployeeErrorKind.Forbidden);
            invitation = await tenantInvitations.IssueAsync(account.Id, client, false, cancellationToken);
        }
        else if (account.Roles.All(role => role.SystemRoleId != SystemRole.BranchAdminId))
        {
            account.AddRole(SystemRole.BranchAdminId);
        }

        var currentAccess = await branchRepository.FindActiveAccessesAsync(organizationId, account.Id, cancellationToken);
        var currentIds = currentAccess.Select(item => item.BranchId).ToHashSet();
        foreach (var branch in branches.Where(item => !currentIds.Contains(item.Id)))
        {
            branchRepository.AddAccess(UserBranchAccess.Grant(organizationId, account.Id, branch.Id, actor.UserId, now));
        }

        branchRepository.AddAuditEvent(CreateAudit(
            invitation is null ? "EMPLOYEE_ADMINISTRATIVE_ACCESS_UPDATED" : "EMPLOYEE_ADMINISTRATOR_INVITED",
            actor,
            organizationId,
            account.Id,
            client,
            now));
        return new ProvisionedAccess(account, organization, invitation);
    }

    private async Task DeliverIfCreatedAsync(
        ProvisionedAccess provisioned,
        Guid organizationId,
        CurrentAccount actor,
        ClientContext client,
        CancellationToken cancellationToken)
    {
        if (provisioned is not { Account: not null, Organization: not null, Invitation: not null })
        {
            return;
        }

        await tenantInvitations.DeliverAsync(
            provisioned.Organization,
            provisioned.Account,
            provisioned.Invitation,
            "administrador de sucursal",
            actor,
            client,
            cancellationToken);
    }

    private async Task<Branch> FindActiveBranchAsync(Guid organizationId, Guid branchId, CancellationToken cancellationToken) =>
        (await branchRepository.FindActiveBranchesAsync(organizationId, [branchId], cancellationToken)).SingleOrDefault()
        ?? throw new EmployeeException(EmployeeErrorCodes.InvalidBranch, "La sucursal no está activa o no pertenece a la organización.");

    private async Task<JobPosition> FindPositionAsync(Guid organizationId, Guid id, CancellationToken cancellationToken) =>
        await repository.FindActiveJobPositionAsync(organizationId, id, cancellationToken)
        ?? throw new EmployeeException(EmployeeErrorCodes.InvalidJobPosition, "El cargo no está activo o no pertenece a la organización.");

    private async Task<IReadOnlyCollection<Branch>> ValidateAdministrativeBranchesAsync(
        Guid organizationId,
        IReadOnlyCollection<Guid>? branchIds,
        CancellationToken cancellationToken)
    {
        var ids = (branchIds ?? []).Where(item => item != Guid.Empty).Distinct().ToArray();
        if (ids.Length == 0)
        {
            throw new EmployeeException(EmployeeErrorCodes.InvalidBranch, "Selecciona al menos una sucursal para el acceso administrativo.");
        }

        var branches = await branchRepository.FindActiveBranchesAsync(organizationId, ids, cancellationToken);
        if (branches.Count != ids.Length)
        {
            throw new EmployeeException(EmployeeErrorCodes.InvalidBranch, "Una sucursal seleccionada no está activa o pertenece a otra organización.");
        }

        return branches;
    }

    private EmployeeResult ToResult(EmployeeQueryItem item) =>
        new(
            item.Employee.Id,
            item.Employee.DocumentType,
            item.Employee.DocumentNumber,
            item.Employee.FirstName,
            item.Employee.LastName,
            item.Assignments.Select(assignment => new EmployeeAssignmentResult(
                assignment.Assignment.Id,
                assignment.Branch.Id,
                assignment.Branch.Name,
                assignment.JobPosition.Id,
                assignment.JobPosition.Name,
                assignment.Assignment.IsPrimary,
                assignment.Assignment.StartedOn)).ToArray(),
            item.Account is null ? null : new EmployeeAdministrativeAccessResult(
                item.Account.Id,
                item.Account.Email,
                item.Account.Status == AccountStatus.Active ? "ACTIVE" : "SUSPENDED",
                tenantInvitations.GetPublicStatus(item.Account, item.Invitation, clock.UtcNow),
                item.Invitation?.ExpiresAt,
                item.AdministrativeBranchIds),
            item.Employee.CreatedAt,
            item.Employee.UpdatedAt);

    private static JobPositionResult ToPositionResult(JobPosition position) =>
        new(position.Id, position.Name, position.Status == JobPositionStatus.Active ? "ACTIVE" : "INACTIVE");

    private (string DocumentType, string DocumentNumber, string FirstName, string LastName) ValidateIdentity(
        string documentType,
        string documentNumber,
        string firstName,
        string lastName) =>
        (
            CleanRequired(documentType, 50, "tipo de documento").ToUpperInvariant(),
            CleanRequired(documentNumber, 50, "número de documento").ToUpperInvariant(),
            CleanRequired(firstName, 100, "nombres"),
            CleanRequired(lastName, 100, "apellidos"));

    private (string Value, string Normalized) ValidateEmail(string? value)
    {
        var email = value?.Trim() ?? string.Empty;
        if (email.Length is 0 or > 320 ||
            !MailAddress.TryCreate(email, out var parsed) ||
            !string.Equals(parsed.Address, email, StringComparison.OrdinalIgnoreCase))
        {
            throw new EmployeeException(EmployeeErrorCodes.InvalidData, "El correo de acceso no es válido.");
        }

        return (email, emailNormalizer.Normalize(email));
    }

    private static Guid EnsureSuperAdministrator(CurrentAccount actor)
    {
        if (actor.AccountType != AccountType.Tenant ||
            actor.OrganizationId is not { } organizationId ||
            !actor.Roles.Contains(SystemRoleCodes.SuperAdmin))
        {
            throw new EmployeeException(EmployeeErrorCodes.Forbidden, "No tienes permiso para administrar trabajadores.", EmployeeErrorKind.Forbidden);
        }

        return organizationId;
    }

    private static string CleanRequired(string? value, int maximumLength, string field)
    {
        var cleaned = value?.Trim() ?? string.Empty;
        if (cleaned.Length is 0 || cleaned.Length > maximumLength)
        {
            throw new EmployeeException(EmployeeErrorCodes.InvalidData, $"El campo {field} no es válido.");
        }

        return cleaned;
    }

    private static string? CleanOptional(string? value, int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var cleaned = value.Trim();
        if (cleaned.Length > maximumLength)
        {
            throw new EmployeeException(EmployeeErrorCodes.InvalidData, $"La búsqueda admite máximo {maximumLength} caracteres.");
        }

        return cleaned;
    }

    private static void ValidatePagination(int page, int pageSize)
    {
        if (page < 1 || pageSize is < 1 or > 100)
        {
            throw new EmployeeException(EmployeeErrorCodes.InvalidData, "La paginación no es válida.");
        }
    }

    private static EmployeeException EmployeeNotFound() =>
        new(EmployeeErrorCodes.NotFound, "El trabajador no existe.", EmployeeErrorKind.NotFound);

    private static EmployeeException DuplicateDocument() =>
        new(EmployeeErrorCodes.DuplicateDocument, "Ya existe un trabajador con ese tipo y número de documento.", EmployeeErrorKind.Conflict);

    private static SecurityAuditEvent CreateAudit(
        string eventType,
        CurrentAccount actor,
        Guid organizationId,
        Guid? accountId,
        ClientContext client,
        DateTimeOffset now,
        Guid? branchId = null) =>
        SecurityAuditEvent.Create(
            eventType,
            "SUCCESS",
            now,
            AccountType.Tenant,
            userAccountId: accountId,
            ipAddress: client.IpAddress,
            userAgent: client.UserAgent,
            organizationId: organizationId,
            actorUserAccountId: actor.UserId,
            branchId: branchId);

    private sealed record ProvisionedAccess(
        UserAccount? Account,
        Organization? Organization,
        IssuedTenantInvitation? Invitation);
}
