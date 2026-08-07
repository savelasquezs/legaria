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
    IBranchService branchService,
    IEmailNormalizer emailNormalizer,
    IPasswordService passwordService,
    ISecureTokenService secureTokenService,
    ITenantInvitationService tenantInvitations,
    IClock clock) : IEmployeeService
{
    public async Task<EmployeeDetailResult> UpdateNotificationContactAsync(Guid id, EmployeeNotificationContactInput input, CurrentAccount actor, CancellationToken cancellationToken)
    {
        var organizationId = EnsureSuperAdministrator(actor);
        var employee = await repository.FindAsync(organizationId, id, cancellationToken) ?? throw EmployeeNotFound();
        var phone = NormalizeNotificationPhone(input.MobilePhone);
        var email = ValidateOptionalContactEmail(input.ContactEmail);
        if (input.WhatsAppConsent && phone is null)
            throw new EmployeeException(EmployeeErrorCodes.InvalidData, "Debes registrar un teléfono antes de autorizar WhatsApp.");
        employee.UpdateNotificationContact(phone, email, input.WhatsAppConsent, clock.UtcNow);
        await repository.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, actor, cancellationToken);
    }

    public async Task<EmployeePage> ListAsync(
        int page,
        int pageSize,
        string? search,
        Guid? branchId,
        Guid? excludeBranchId,
        CurrentAccount actor,
        CancellationToken cancellationToken)
    {
        var organizationId = EnsureTenantAdministrator(actor);
        ValidatePagination(page, pageSize);
        var isSuperAdministrator = IsSuperAdministrator(actor);
        if (!isSuperAdministrator && (branchId is null || excludeBranchId is not null))
        {
            throw new EmployeeException(
                EmployeeErrorCodes.Forbidden,
                "Debes consultar los trabajadores desde una sucursal autorizada.",
                EmployeeErrorKind.Forbidden);
        }

        if (branchId is { } included && await branchRepository.FindBranchAsync(organizationId, included, cancellationToken) is null)
        {
            throw EmployeeNotFound();
        }

        if (!isSuperAdministrator && branchId is { } assignedBranch &&
            await branchRepository.FindBranchDetailsAsync(organizationId, assignedBranch, actor.UserId, cancellationToken) is null)
        {
            throw EmployeeNotFound();
        }

        var (items, total) = await repository.ListAsync(
            organizationId,
            branchId,
            excludeBranchId,
            isSuperAdministrator ? null : actor.EmployeeId,
            (page - 1) * pageSize,
            pageSize,
            CleanOptional(search, 200),
            cancellationToken);
        return new EmployeePage(
            items.Select(item => ToResult(item, isSuperAdministrator, isSuperAdministrator ? null : branchId)).ToArray(),
            page,
            pageSize,
            total,
            total == 0 ? 0 : (int)Math.Ceiling(total / (double)pageSize));
    }

    public async Task<EmployeeDetailResult> GetAsync(Guid id, CurrentAccount actor, CancellationToken cancellationToken)
    {
        var organizationId = EnsureTenantAdministrator(actor);
        if (!IsSuperAdministrator(actor) && actor.EmployeeId == id)
        {
            throw new EmployeeException(
                EmployeeErrorCodes.Forbidden,
                "No puedes consultar tu propio expediente laboral.",
                EmployeeErrorKind.Forbidden);
        }

        var detail = await repository.FindEmploymentDetailsAsync(organizationId, id, cancellationToken)
            ?? throw EmployeeNotFound();
        if (IsSuperAdministrator(actor))
        {
            return ToDetailResult(detail);
        }

        var branchIds = (await branchRepository.FindActiveAccessesAsync(
            organizationId,
            actor.UserId,
            cancellationToken)).Select(item => item.BranchId).ToHashSet();
        if (!detail.Relationships.SelectMany(item => item.Assignments).Any(item =>
                item.Assignment.EndedOn is null && branchIds.Contains(item.Assignment.BranchId)))
        {
            throw EmployeeNotFound();
        }

        var visibleRelationships = detail.Relationships
            .Select(relationship => new EmploymentRelationshipQueryItem(
                relationship.Relationship,
                relationship.Assignments.Where(item => branchIds.Contains(item.Assignment.BranchId)).ToArray()))
            .Where(relationship => relationship.Assignments.Count > 0)
            .ToArray();
        return ToDetailResult(new EmployeeDetailQueryItem(
            detail.Employee,
            visibleRelationships,
            null,
            null,
            []));
    }

    public async Task<EmployeeDetailResult> CreateAsync(
        Guid branchId,
        CreateEmployeeInput input,
        CurrentAccount actor,
        ClientContext client,
        CancellationToken cancellationToken)
    {
        var organizationId = EnsureSuperAdministrator(actor);
        var branch = await FindActiveBranchAsync(organizationId, branchId, cancellationToken);
        var position = await FindPositionAsync(organizationId, input.JobPositionId, cancellationToken);
        ValidateOperationalDate(input.StartedOn, "fecha de inicio");
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
        var phone = NormalizeNotificationPhone(input.MobilePhone);
        var contactEmail = ValidateOptionalContactEmail(input.ContactEmail);
        if (input.WhatsAppConsent && phone is null)
            throw new EmployeeException(EmployeeErrorCodes.InvalidData, "Debes registrar un teléfono antes de autorizar WhatsApp.");
        var employee = Employee.Create(
            organizationId,
            identity.DocumentType,
            identity.DocumentNumber,
            identity.FirstName,
            identity.LastName,
            now);
        employee.UpdateNotificationContact(phone, contactEmail, input.WhatsAppConsent, now);
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

    public async Task<EmployeeDetailResult> AssignAsync(
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
        ValidateOperationalDate(input.StartedOn, "fecha de inicio");
        var now = clock.UtcNow;
        var relationship = await repository.FindActiveRelationshipAsync(organizationId, employeeId, cancellationToken);
        if (relationship is null)
        {
            var latest = await repository.FindLatestRelationshipAsync(organizationId, employeeId, cancellationToken);
            if (latest?.EndedOn is { } previousEnd && input.StartedOn <= previousEnd)
            {
                throw InvalidDate("La nueva relación laboral debe comenzar después de la relación anterior.");
            }

            relationship = EmploymentRelationship.Create(organizationId, employeeId, input.StartedOn, now);
            repository.AddRelationship(relationship);
        }
        else
        {
            if (input.StartedOn < relationship.StartedOn)
            {
                throw InvalidDate("La asignación no puede comenzar antes de la relación laboral.");
            }

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

        if (await repository.AssignmentPeriodOverlapsAsync(
            organizationId,
            relationship.Id,
            branchId,
            input.StartedOn,
            null,
            null,
            cancellationToken))
        {
            throw DuplicateAssignment();
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

    public async Task<EmployeeDetailResult> GrantAdministrativeAccessAsync(
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
        string? status,
        CurrentAccount actor,
        CancellationToken cancellationToken)
    {
        var organizationId = EnsureSuperAdministrator(actor);
        return (await repository.ListJobPositionsAsync(organizationId, ParsePositionStatus(status), cancellationToken))
            .Select(item => ToPositionResult(item.JobPosition, item.RequiredDocumentCount))
            .ToArray();
    }

    public async Task<JobPositionResult> CreateJobPositionAsync(
        JobPositionInput input,
        CurrentAccount actor,
        CancellationToken cancellationToken)
    {
        var organizationId = EnsureSuperAdministrator(actor);
        var (name, normalizedName) = CleanPositionName(input.Name);
        if (await repository.JobPositionNameExistsAsync(organizationId, normalizedName, null, cancellationToken))
        {
            throw DuplicatePosition();
        }

        var position = JobPosition.Create(organizationId, name, normalizedName, clock.UtcNow);
        repository.AddJobPosition(position);
        await repository.SaveChangesAsync(cancellationToken);
        return ToPositionResult(position, 0);
    }

    public async Task<JobPositionResult> UpdateJobPositionAsync(
        Guid id,
        JobPositionInput input,
        CurrentAccount actor,
        CancellationToken cancellationToken)
    {
        var organizationId = EnsureSuperAdministrator(actor);
        var position = await repository.FindJobPositionAsync(organizationId, id, cancellationToken)
            ?? throw PositionNotFound();
        var (name, normalizedName) = CleanPositionName(input.Name);
        if (await repository.JobPositionNameExistsAsync(organizationId, normalizedName, id, cancellationToken))
        {
            throw DuplicatePosition();
        }

        if (!position.Rename(name, normalizedName, clock.UtcNow))
        {
            throw new EmployeeException(EmployeeErrorCodes.InvalidData, "El cargo no tiene cambios.", EmployeeErrorKind.Conflict);
        }

        await repository.SaveChangesAsync(cancellationToken);
        return ToPositionResult(
            position,
            await repository.CountJobPositionDocumentRequirementsAsync(organizationId, position.Id, cancellationToken));
    }

    public Task<JobPositionResult> DeactivateJobPositionAsync(
        Guid id,
        CurrentAccount actor,
        CancellationToken cancellationToken) =>
        ChangePositionStatusAsync(id, true, actor, cancellationToken);

    public Task<JobPositionResult> ReactivateJobPositionAsync(
        Guid id,
        CurrentAccount actor,
        CancellationToken cancellationToken) =>
        ChangePositionStatusAsync(id, false, actor, cancellationToken);

    public async Task<JobPositionDocumentRequirementsResult> GetJobPositionDocumentRequirementsAsync(
        Guid id,
        CurrentAccount actor,
        CancellationToken cancellationToken)
    {
        var organizationId = EnsureSuperAdministrator(actor);
        _ = await repository.FindJobPositionAsync(organizationId, id, cancellationToken)
            ?? throw PositionNotFound();
        return new JobPositionDocumentRequirementsResult(
            id,
            await repository.ListJobPositionDocumentRequirementIdsAsync(organizationId, id, cancellationToken));
    }

    public async Task<JobPositionDocumentRequirementsResult> UpdateJobPositionDocumentRequirementsAsync(
        Guid id,
        JobPositionDocumentRequirementsInput input,
        CurrentAccount actor,
        CancellationToken cancellationToken)
    {
        var organizationId = EnsureSuperAdministrator(actor);
        _ = await repository.FindJobPositionAsync(organizationId, id, cancellationToken)
            ?? throw PositionNotFound();
        var documentTypeIds = (input.DocumentTypeIds ?? [])
            .Distinct()
            .ToArray();
        if (documentTypeIds.Any(documentTypeId => documentTypeId == Guid.Empty) ||
            !await repository.AreAvailableEmployeeDocumentTypesAsync(organizationId, documentTypeIds, cancellationToken))
        {
            throw new EmployeeException(
                EmployeeErrorCodes.InvalidDocumentRequirement,
                "Solo puedes exigir tipos de documento activos para trabajadores y pertenecientes a la organización.");
        }

        await repository.ReplaceJobPositionDocumentRequirementsAsync(
            organizationId,
            id,
            documentTypeIds,
            cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        return new JobPositionDocumentRequirementsResult(id, documentTypeIds);
    }

    public async Task<EmployeeDetailResult> EndRelationshipAsync(
        Guid employeeId,
        Guid relationshipId,
        EndEmploymentRelationshipInput input,
        CurrentAccount actor,
        ClientContext client,
        CancellationToken cancellationToken)
    {
        var organizationId = EnsureSuperAdministrator(actor);
        ValidateOperationalDate(input.EndedOn, "fecha de finalización");
        var relationship = await repository.FindRelationshipAsync(organizationId, employeeId, relationshipId, cancellationToken)
            ?? throw RelationshipNotFound();
        if (relationship.EndedOn is not null)
        {
            throw InvalidRelationshipState("La relación laboral ya está finalizada.");
        }

        if (input.EndedOn < relationship.StartedOn)
        {
            throw InvalidDate("La relación laboral no puede terminar antes de comenzar.");
        }

        var assignments = await repository.FindActiveAssignmentsAsync(organizationId, relationship.Id, cancellationToken);
        if (assignments.Any(item => item.StartedOn > input.EndedOn))
        {
            throw InvalidDate("La fecha de finalización es anterior a una asignación activa.");
        }

        await using var transaction = await repository.BeginTransactionAsync(cancellationToken);
        var now = clock.UtcNow;
        foreach (var assignment in assignments)
        {
            assignment.End(input.EndedOn, now);
        }

        relationship.End(input.EndedOn, now);
        branchRepository.AddAuditEvent(CreateAudit(
            "EMPLOYMENT_RELATIONSHIP_ENDED",
            actor,
            organizationId,
            null,
            client,
            now));
        await repository.SaveChangesAsync(cancellationToken);

        var account = await repository.FindLinkedAccountAsync(organizationId, employeeId, cancellationToken);
        if (account is { Status: AccountStatus.Active } && account.Roles.Any(item => item.SystemRoleId == SystemRole.BranchAdminId))
        {
            await branchService.SuspendAdministratorAsync(account.Id, actor, client, cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return await GetAsync(employeeId, actor, cancellationToken);
    }

    public async Task<EmployeeDetailResult> EndAssignmentAsync(
        Guid employeeId,
        Guid assignmentId,
        EndEmployeeAssignmentInput input,
        CurrentAccount actor,
        ClientContext client,
        CancellationToken cancellationToken)
    {
        var organizationId = EnsureSuperAdministrator(actor);
        ValidateOperationalDate(input.EndedOn, "fecha de finalización");
        var assignment = await repository.FindAssignmentAsync(organizationId, employeeId, assignmentId, cancellationToken)
            ?? throw AssignmentNotFound();
        if (assignment.EndedOn is not null)
        {
            throw InvalidAssignmentState("La asignación ya está finalizada.");
        }

        if (input.EndedOn < assignment.StartedOn)
        {
            throw InvalidDate("La asignación no puede terminar antes de comenzar.");
        }

        assignment.End(input.EndedOn, clock.UtcNow);
        branchRepository.AddAuditEvent(CreateAudit("EMPLOYEE_ASSIGNMENT_ENDED", actor, organizationId, null, client, clock.UtcNow, assignment.BranchId));
        await repository.SaveChangesAsync(cancellationToken);
        return await GetAsync(employeeId, actor, cancellationToken);
    }

    public async Task<EmployeeDetailResult> TransitionAssignmentAsync(
        Guid employeeId,
        Guid assignmentId,
        TransitionEmployeeAssignmentInput input,
        CurrentAccount actor,
        ClientContext client,
        CancellationToken cancellationToken)
    {
        var organizationId = EnsureSuperAdministrator(actor);
        ValidateOperationalDate(input.EffectiveOn, "fecha efectiva");
        var assignment = await repository.FindAssignmentAsync(organizationId, employeeId, assignmentId, cancellationToken)
            ?? throw AssignmentNotFound();
        if (assignment.EndedOn is not null)
        {
            throw InvalidAssignmentState("Solo una asignación activa puede cambiarse.");
        }

        if (input.EffectiveOn <= assignment.StartedOn)
        {
            throw InvalidDate("La fecha efectiva debe ser posterior al inicio de la asignación actual.");
        }

        if (input.BranchId == assignment.BranchId && input.JobPositionId == assignment.JobPositionId)
        {
            throw InvalidAssignmentState("Selecciona un cargo o una sucursal diferente.");
        }

        var relationship = await repository.FindRelationshipAsync(
            organizationId,
            employeeId,
            assignment.EmploymentRelationshipId,
            cancellationToken) ?? throw RelationshipNotFound();
        if (relationship.EndedOn is not null)
        {
            throw InvalidRelationshipState("La relación laboral está finalizada.");
        }

        var branch = await FindActiveBranchAsync(organizationId, input.BranchId, cancellationToken);
        var position = await FindPositionAsync(organizationId, input.JobPositionId, cancellationToken);
        if (await repository.AssignmentPeriodOverlapsAsync(
            organizationId,
            relationship.Id,
            branch.Id,
            input.EffectiveOn,
            null,
            assignment.Id,
            cancellationToken))
        {
            throw DuplicateAssignment();
        }

        await using var transaction = await repository.BeginTransactionAsync(cancellationToken);
        var now = clock.UtcNow;
        assignment.End(input.EffectiveOn.AddDays(-1), now);
        await repository.SaveChangesAsync(cancellationToken);
        repository.AddAssignment(EmployeeAssignment.Create(
            organizationId,
            relationship.Id,
            branch.Id,
            position.Id,
            assignment.IsPrimary,
            input.EffectiveOn,
            now));
        branchRepository.AddAuditEvent(CreateAudit("EMPLOYEE_ASSIGNMENT_TRANSITIONED", actor, organizationId, null, client, now, branch.Id));
        await repository.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await GetAsync(employeeId, actor, cancellationToken);
    }

    public async Task<EmployeeDetailResult> MakePrimaryAssignmentAsync(
        Guid employeeId,
        Guid assignmentId,
        CurrentAccount actor,
        ClientContext client,
        CancellationToken cancellationToken)
    {
        var organizationId = EnsureSuperAdministrator(actor);
        var assignment = await repository.FindAssignmentAsync(organizationId, employeeId, assignmentId, cancellationToken)
            ?? throw AssignmentNotFound();
        if (assignment.EndedOn is not null || assignment.IsPrimary)
        {
            throw InvalidAssignmentState(assignment.IsPrimary
                ? "La asignación ya es la principal."
                : "Solo una asignación activa puede marcarse como principal.");
        }

        await using var transaction = await repository.BeginTransactionAsync(cancellationToken);
        var now = clock.UtcNow;
        var activeAssignments = await repository.FindActiveAssignmentsAsync(
            organizationId,
            assignment.EmploymentRelationshipId,
            cancellationToken);
        foreach (var current in activeAssignments.Where(item => item.IsPrimary))
        {
            current.SetPrimary(false, now);
        }

        await repository.SaveChangesAsync(cancellationToken);
        assignment.SetPrimary(true, now);
        branchRepository.AddAuditEvent(CreateAudit("EMPLOYEE_PRIMARY_ASSIGNMENT_CHANGED", actor, organizationId, null, client, now, assignment.BranchId));
        await repository.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await GetAsync(employeeId, actor, cancellationToken);
    }

    private async Task<JobPositionResult> ChangePositionStatusAsync(
        Guid id,
        bool deactivate,
        CurrentAccount actor,
        CancellationToken cancellationToken)
    {
        var organizationId = EnsureSuperAdministrator(actor);
        var position = await repository.FindJobPositionAsync(organizationId, id, cancellationToken)
            ?? throw PositionNotFound();
        var changed = deactivate ? position.Deactivate(clock.UtcNow) : position.Reactivate(clock.UtcNow);
        if (!changed)
        {
            throw new EmployeeException(
                EmployeeErrorCodes.JobPositionInvalidStatus,
                deactivate ? "El cargo ya está inactivo." : "El cargo ya está activo.",
                EmployeeErrorKind.Conflict);
        }

        await repository.SaveChangesAsync(cancellationToken);
        return ToPositionResult(
            position,
            await repository.CountJobPositionDocumentRequirementsAsync(organizationId, position.Id, cancellationToken));
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

    private EmployeeResult ToResult(
        EmployeeQueryItem item,
        bool includeAdministrativeAccess,
        Guid? visibleBranchId) =>
        new(
            item.Employee.Id,
            item.Employee.DocumentType,
            item.Employee.DocumentNumber,
            item.Employee.FirstName,
            item.Employee.LastName,
            item.Employee.MobilePhone,
            item.Employee.ContactEmail,
            item.Employee.WhatsAppConsentAt,
            item.Assignments.Where(assignment => visibleBranchId is null || assignment.Branch.Id == visibleBranchId)
                .Select(assignment => new EmployeeAssignmentResult(
                assignment.Assignment.Id,
                assignment.Assignment.EmploymentRelationshipId,
                assignment.Branch.Id,
                assignment.Branch.Name,
                assignment.JobPosition.Id,
                assignment.JobPosition.Name,
                assignment.Assignment.IsPrimary,
                assignment.Assignment.StartedOn,
                assignment.Assignment.EndedOn,
                assignment.Assignment.EndedOn is null ? "ACTIVE" : "ENDED")).ToArray(),
            !includeAdministrativeAccess || item.Account is null ? null : new EmployeeAdministrativeAccessResult(
                item.Account.Id,
                item.Account.Email,
                item.Account.Status == AccountStatus.Active ? "ACTIVE" : "SUSPENDED",
                tenantInvitations.GetPublicStatus(item.Account, item.Invitation, clock.UtcNow),
                item.Invitation?.ExpiresAt,
                item.AdministrativeBranchIds),
            item.Employee.CreatedAt,
            item.Employee.UpdatedAt);

    private EmployeeDetailResult ToDetailResult(EmployeeDetailQueryItem item) =>
        new(
            item.Employee.Id,
            item.Employee.DocumentType,
            item.Employee.DocumentNumber,
            item.Employee.FirstName,
            item.Employee.LastName,
            item.Employee.MobilePhone,
            item.Employee.ContactEmail,
            item.Employee.WhatsAppConsentAt,
            item.Relationships.Select(relationship => new EmploymentRelationshipResult(
                relationship.Relationship.Id,
                relationship.Relationship.StartedOn,
                relationship.Relationship.EndedOn,
                relationship.Relationship.EndedOn is null ? "ACTIVE" : "ENDED",
                relationship.Assignments.Select(assignment => new EmployeeAssignmentResult(
                    assignment.Assignment.Id,
                    assignment.Assignment.EmploymentRelationshipId,
                    assignment.Branch.Id,
                    assignment.Branch.Name,
                    assignment.JobPosition.Id,
                    assignment.JobPosition.Name,
                    assignment.Assignment.IsPrimary,
                    assignment.Assignment.StartedOn,
                    assignment.Assignment.EndedOn,
                    assignment.Assignment.EndedOn is null ? "ACTIVE" : "ENDED")).ToArray())).ToArray(),
            item.Account is null ? null : new EmployeeAdministrativeAccessResult(
                item.Account.Id,
                item.Account.Email,
                item.Account.Status == AccountStatus.Active ? "ACTIVE" : "SUSPENDED",
                tenantInvitations.GetPublicStatus(item.Account, item.Invitation, clock.UtcNow),
                item.Invitation?.ExpiresAt,
                item.AdministrativeBranchIds),
            item.Employee.CreatedAt,
            item.Employee.UpdatedAt);

    private static JobPositionResult ToPositionResult(JobPosition position, int requiredDocumentCount) =>
        new(
            position.Id,
            position.Name,
            position.Status == JobPositionStatus.Active ? "ACTIVE" : "INACTIVE",
            requiredDocumentCount);

    private static JobPositionStatus? ParsePositionStatus(string? value) => value?.Trim().ToUpperInvariant() switch
    {
        null or "" or "ACTIVE" => JobPositionStatus.Active,
        "INACTIVE" => JobPositionStatus.Inactive,
        "ALL" => null,
        _ => throw new EmployeeException(EmployeeErrorCodes.InvalidData, "El estado del cargo no es válido.")
    };

    private static (string Name, string NormalizedName) CleanPositionName(string? value)
    {
        var name = string.Join(' ', CleanRequired(value, 150, "nombre del cargo")
            .Split(' ', StringSplitOptions.RemoveEmptyEntries));
        return (name, name.ToUpperInvariant());
    }

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

    private static Guid EnsureTenantAdministrator(CurrentAccount actor)
    {
        if (actor.AccountType != AccountType.Tenant ||
            actor.OrganizationId is not { } organizationId ||
            !actor.Roles.Any(role => role is SystemRoleCodes.SuperAdmin or SystemRoleCodes.BranchAdmin))
        {
            throw new EmployeeException(EmployeeErrorCodes.Forbidden, "No tienes permiso para consultar trabajadores.", EmployeeErrorKind.Forbidden);
        }

        return organizationId;
    }

    private static bool IsSuperAdministrator(CurrentAccount actor) =>
        actor.Roles.Contains(SystemRoleCodes.SuperAdmin);

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

    private static string? NormalizeNotificationPhone(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var clean = value.Trim().Replace(" ", string.Empty);
        if (!System.Text.RegularExpressions.Regex.IsMatch(clean, @"^\+[1-9]\d{7,14}$"))
            throw new EmployeeException(EmployeeErrorCodes.InvalidData, "El teléfono debe usar formato internacional, por ejemplo +573001234567.");
        return clean;
    }

    private static string? ValidateOptionalContactEmail(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var email = value.Trim();
        if (email.Length > 320 || !MailAddress.TryCreate(email, out var parsed) || !string.Equals(parsed.Address, email, StringComparison.OrdinalIgnoreCase))
            throw new EmployeeException(EmployeeErrorCodes.InvalidData, "El correo de contacto no es válido.");
        return email;
    }

    private static void ValidatePagination(int page, int pageSize)
    {
        if (page < 1 || pageSize is < 1 or > 100)
        {
            throw new EmployeeException(EmployeeErrorCodes.InvalidData, "La paginación no es válida.");
        }
    }

    private void ValidateOperationalDate(DateOnly value, string field)
    {
        var today = DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);
        if (value == default || value > today)
        {
            throw InvalidDate($"La {field} no puede ser futura.");
        }
    }

    private static EmployeeException EmployeeNotFound() =>
        new(EmployeeErrorCodes.NotFound, "El trabajador no existe.", EmployeeErrorKind.NotFound);

    private static EmployeeException DuplicateDocument() =>
        new(EmployeeErrorCodes.DuplicateDocument, "Ya existe un trabajador con ese tipo y número de documento.", EmployeeErrorKind.Conflict);

    private static EmployeeException DuplicateAssignment() =>
        new(EmployeeErrorCodes.DuplicateAssignment, "La asignación se superpone con otro periodo de la misma sucursal.", EmployeeErrorKind.Conflict);

    private static EmployeeException DuplicatePosition() =>
        new(EmployeeErrorCodes.JobPositionDuplicateName, "Ya existe un cargo con ese nombre.", EmployeeErrorKind.Conflict);

    private static EmployeeException PositionNotFound() =>
        new(EmployeeErrorCodes.JobPositionNotFound, "El cargo no existe.", EmployeeErrorKind.NotFound);

    private static EmployeeException RelationshipNotFound() =>
        new(EmployeeErrorCodes.RelationshipNotFound, "La relación laboral no existe.", EmployeeErrorKind.NotFound);

    private static EmployeeException AssignmentNotFound() =>
        new(EmployeeErrorCodes.AssignmentNotFound, "La asignación no existe.", EmployeeErrorKind.NotFound);

    private static EmployeeException InvalidRelationshipState(string message) =>
        new(EmployeeErrorCodes.RelationshipInvalidState, message, EmployeeErrorKind.Conflict);

    private static EmployeeException InvalidAssignmentState(string message) =>
        new(EmployeeErrorCodes.AssignmentInvalidState, message, EmployeeErrorKind.Conflict);

    private static EmployeeException InvalidDate(string message) =>
        new(EmployeeErrorCodes.InvalidDate, message);

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
