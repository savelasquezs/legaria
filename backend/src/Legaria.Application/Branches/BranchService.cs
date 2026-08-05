using System.Net.Mail;
using Legaria.Application.Authentication;
using Legaria.Application.Organizations;
using Legaria.Domain.Authentication;
using Legaria.Domain.Tenancy;

namespace Legaria.Application.Branches;

public sealed class BranchService(
    IBranchRepository repository,
    IEmailNormalizer emailNormalizer,
    IPasswordService passwordService,
    ISecureTokenService secureTokenService,
    ITenantInvitationService tenantInvitations,
    IClock clock) : IBranchService
{
    public async Task<BranchPage> ListBranchesAsync(
        int page,
        int pageSize,
        string? search,
        string? status,
        CurrentAccount actor,
        CancellationToken cancellationToken)
    {
        var organizationId = EnsureTenantActor(actor);
        ValidatePagination(page, pageSize);
        var (items, total) = await repository.ListBranchesAsync(
            organizationId,
            IsSuperAdministrator(actor) ? null : actor.UserId,
            (page - 1) * pageSize,
            pageSize,
            CleanOptional(search, 200),
            ParseBranchStatus(status),
            cancellationToken);
        return new BranchPage(
            items.Select(ToBranchResult).ToArray(),
            page,
            pageSize,
            total,
            total == 0 ? 0 : (int)Math.Ceiling(total / (double)pageSize));
    }

    public async Task<BranchResult> GetBranchAsync(
        Guid id,
        CurrentAccount actor,
        CancellationToken cancellationToken)
    {
        var organizationId = EnsureTenantActor(actor);
        var result = await repository.FindBranchDetailsAsync(
            organizationId,
            id,
            IsSuperAdministrator(actor) ? null : actor.UserId,
            cancellationToken);
        return ToBranchResult(result ?? throw BranchNotFound());
    }

    public async Task<BranchResult> CreateBranchAsync(
        BranchInput input,
        CurrentAccount actor,
        ClientContext client,
        CancellationToken cancellationToken)
    {
        var organizationId = EnsureSuperAdministrator(actor);
        var validated = await ValidateBranchAsync(input, organizationId, null, cancellationToken);
        var now = clock.UtcNow;
        var branch = Branch.Create(
            organizationId,
            validated.Name,
            validated.NormalizedName,
            validated.ContactEmail,
            validated.Phone,
            validated.Address,
            validated.MunicipalityCode,
            now);
        repository.AddBranch(branch);
        repository.AddAuditEvent(CreateAudit("BRANCH_CREATED", actor, organizationId, null, client, now, branch.Id));
        await repository.SaveChangesAsync(cancellationToken);
        return await GetBranchAsync(branch.Id, actor, cancellationToken);
    }

    public async Task<BranchResult> UpdateBranchAsync(
        Guid id,
        BranchInput input,
        CurrentAccount actor,
        ClientContext client,
        CancellationToken cancellationToken)
    {
        var organizationId = EnsureSuperAdministrator(actor);
        var branch = await repository.FindBranchAsync(organizationId, id, cancellationToken)
            ?? throw BranchNotFound();
        var validated = await ValidateBranchAsync(input, organizationId, id, cancellationToken);
        var now = clock.UtcNow;
        branch.Update(
            validated.Name,
            validated.NormalizedName,
            validated.ContactEmail,
            validated.Phone,
            validated.Address,
            validated.MunicipalityCode,
            now);
        repository.AddAuditEvent(CreateAudit("BRANCH_UPDATED", actor, organizationId, null, client, now, branch.Id));
        await repository.SaveChangesAsync(cancellationToken);
        return await GetBranchAsync(id, actor, cancellationToken);
    }

    public Task<BranchResult> DeactivateBranchAsync(
        Guid id,
        CurrentAccount actor,
        ClientContext client,
        CancellationToken cancellationToken) =>
        ChangeBranchStatusAsync(id, true, actor, client, cancellationToken);

    public Task<BranchResult> ReactivateBranchAsync(
        Guid id,
        CurrentAccount actor,
        ClientContext client,
        CancellationToken cancellationToken) =>
        ChangeBranchStatusAsync(id, false, actor, client, cancellationToken);

    public async Task<BranchAdministratorPage> ListAdministratorsAsync(
        int page,
        int pageSize,
        string? search,
        string? status,
        CurrentAccount actor,
        CancellationToken cancellationToken)
    {
        var organizationId = EnsureSuperAdministrator(actor);
        ValidatePagination(page, pageSize);
        var (items, total) = await repository.ListAdministratorsAsync(
            organizationId,
            (page - 1) * pageSize,
            pageSize,
            CleanOptional(search, 200),
            ParseAccountStatus(status),
            cancellationToken);
        return new BranchAdministratorPage(
            items.Select(ToAdministratorResult).ToArray(),
            page,
            pageSize,
            total,
            total == 0 ? 0 : (int)Math.Ceiling(total / (double)pageSize));
    }

    public async Task<BranchAdministratorResult> GetAdministratorAsync(
        Guid id,
        CurrentAccount actor,
        CancellationToken cancellationToken)
    {
        var organizationId = EnsureSuperAdministrator(actor);
        var result = await repository.FindAdministratorDetailsAsync(organizationId, id, cancellationToken)
            ?? throw AdministratorNotFound();
        return ToAdministratorResult(result);
    }

    public async Task<BranchAdministratorResult> CreateAdministratorAsync(
        BranchAdministratorInput input,
        CurrentAccount actor,
        ClientContext client,
        CancellationToken cancellationToken)
    {
        var organizationId = EnsureSuperAdministrator(actor);
        var organization = await repository.FindOrganizationAsync(organizationId, cancellationToken)
            ?? throw new BranchException(BranchErrorCodes.Forbidden, "La organización no está disponible.", BranchErrorKind.Forbidden);
        var admin = ValidateAdministrator(input);
        if (await repository.EmailExistsAsync(admin.NormalizedEmail, null, cancellationToken))
        {
            throw DuplicateEmail();
        }

        var branches = await ValidateBranchIdsAsync(organizationId, input.BranchIds, cancellationToken);
        var now = clock.UtcNow;
        var account = UserAccount.Create(
            organizationId,
            null,
            admin.Email,
            admin.NormalizedEmail,
            passwordService.Hash(secureTokenService.GenerateToken()),
            admin.FirstName,
            admin.LastName,
            secureTokenService.GenerateSecurityStamp(),
            false,
            now);
        account.AddRole(SystemRole.BranchAdminId);
        repository.AddUserAccount(account);
        repository.AddAccountEmail(AccountEmail.ForTenant(admin.NormalizedEmail, account.Id, now));
        foreach (var branch in branches)
        {
            repository.AddAccess(UserBranchAccess.Grant(
                organizationId,
                account.Id,
                branch.Id,
                actor.UserId,
                now));
        }

        var invitation = await tenantInvitations.IssueAsync(
            account.Id,
            client,
            false,
            cancellationToken);
        repository.AddAuditEvent(CreateAudit(
            "BRANCH_ADMINISTRATOR_INVITED",
            actor,
            organizationId,
            account.Id,
            client,
            now));
        await repository.SaveChangesAsync(cancellationToken);
        await tenantInvitations.DeliverAsync(
            organization,
            account,
            invitation,
            "administrador de sucursal",
            actor,
            client,
            cancellationToken);
        return await GetAdministratorAsync(account.Id, actor, cancellationToken);
    }

    public async Task<BranchAdministratorResult> UpdatePendingAdministratorAsync(
        Guid id,
        BranchAdministratorInput input,
        CurrentAccount actor,
        ClientContext client,
        CancellationToken cancellationToken)
    {
        var organizationId = EnsureSuperAdministrator(actor);
        var organization = await repository.FindOrganizationAsync(organizationId, cancellationToken)
            ?? throw AdministratorNotFound();
        var account = await repository.FindAdministratorAsync(organizationId, id, cancellationToken)
            ?? throw AdministratorNotFound();
        EnsurePending(account);
        EnsureActive(account);
        var admin = ValidateAdministrator(input);
        if (await repository.EmailExistsAsync(admin.NormalizedEmail, account.Id, cancellationToken))
        {
            throw DuplicateEmail();
        }

        var branches = await ValidateBranchIdsAsync(organizationId, input.BranchIds, cancellationToken);
        var now = clock.UtcNow;
        if (!string.Equals(account.NormalizedEmail, admin.NormalizedEmail, StringComparison.Ordinal))
        {
            var reservation = await repository.FindAccountEmailAsync(account.Id, cancellationToken)
                ?? throw new InvalidOperationException("La cuenta no tiene una reserva global de correo.");
            repository.RemoveAccountEmail(reservation);
            repository.AddAccountEmail(AccountEmail.ForTenant(admin.NormalizedEmail, account.Id, now));
        }

        account.UpdatePendingIdentity(
            admin.Email,
            admin.NormalizedEmail,
            admin.FirstName,
            admin.LastName,
            secureTokenService.GenerateSecurityStamp(),
            now);
        await ReplaceAccessesAsync(organizationId, account.Id, branches, actor.UserId, now, cancellationToken);
        var invitation = await tenantInvitations.IssueAsync(account.Id, client, true, cancellationToken);
        repository.AddAuditEvent(CreateAudit(
            "BRANCH_ADMINISTRATOR_PENDING_PROFILE_UPDATED",
            actor,
            organizationId,
            account.Id,
            client,
            now));
        await repository.SaveChangesAsync(cancellationToken);
        await tenantInvitations.DeliverAsync(
            organization,
            account,
            invitation,
            "administrador de sucursal",
            actor,
            client,
            cancellationToken);
        return await GetAdministratorAsync(id, actor, cancellationToken);
    }

    public async Task<BranchAdministratorResult> UpdateAssignmentsAsync(
        Guid id,
        UpdateBranchAssignmentsRequest request,
        CurrentAccount actor,
        ClientContext client,
        CancellationToken cancellationToken)
    {
        var organizationId = EnsureSuperAdministrator(actor);
        var account = await repository.FindAdministratorAsync(organizationId, id, cancellationToken)
            ?? throw AdministratorNotFound();
        var branches = await ValidateBranchIdsAsync(organizationId, request.BranchIds, cancellationToken);
        var now = clock.UtcNow;
        await ReplaceAccessesAsync(organizationId, account.Id, branches, actor.UserId, now, cancellationToken);
        repository.AddAuditEvent(CreateAudit(
            "BRANCH_ADMINISTRATOR_ACCESS_UPDATED",
            actor,
            organizationId,
            account.Id,
            client,
            now));
        await repository.SaveChangesAsync(cancellationToken);
        return await GetAdministratorAsync(id, actor, cancellationToken);
    }

    public async Task<BranchAdministratorResult> ResendInvitationAsync(
        Guid id,
        CurrentAccount actor,
        ClientContext client,
        CancellationToken cancellationToken)
    {
        var organizationId = EnsureSuperAdministrator(actor);
        var organization = await repository.FindOrganizationAsync(organizationId, cancellationToken)
            ?? throw AdministratorNotFound();
        var account = await repository.FindAdministratorAsync(organizationId, id, cancellationToken)
            ?? throw AdministratorNotFound();
        EnsurePending(account);
        EnsureActive(account);
        var now = clock.UtcNow;
        var invitation = await tenantInvitations.IssueAsync(account.Id, client, true, cancellationToken);
        repository.AddAuditEvent(CreateAudit(
            "BRANCH_ADMINISTRATOR_INVITATION_REISSUED",
            actor,
            organizationId,
            account.Id,
            client,
            now));
        await repository.SaveChangesAsync(cancellationToken);
        await tenantInvitations.DeliverAsync(
            organization,
            account,
            invitation,
            "administrador de sucursal",
            actor,
            client,
            cancellationToken);
        return await GetAdministratorAsync(id, actor, cancellationToken);
    }

    public Task<BranchAdministratorResult> SuspendAdministratorAsync(
        Guid id,
        CurrentAccount actor,
        ClientContext client,
        CancellationToken cancellationToken) =>
        ChangeAdministratorStatusAsync(id, true, actor, client, cancellationToken);

    public Task<BranchAdministratorResult> ReactivateAdministratorAsync(
        Guid id,
        CurrentAccount actor,
        ClientContext client,
        CancellationToken cancellationToken) =>
        ChangeAdministratorStatusAsync(id, false, actor, client, cancellationToken);

    private async Task<BranchResult> ChangeBranchStatusAsync(
        Guid id,
        bool deactivate,
        CurrentAccount actor,
        ClientContext client,
        CancellationToken cancellationToken)
    {
        var organizationId = EnsureSuperAdministrator(actor);
        var branch = await repository.FindBranchAsync(organizationId, id, cancellationToken)
            ?? throw BranchNotFound();
        var now = clock.UtcNow;
        var changed = deactivate ? branch.Deactivate(now) : branch.Reactivate(now);
        if (!changed)
        {
            throw new BranchException(
                BranchErrorCodes.InvalidStatusTransition,
                deactivate ? "La sucursal ya está inactiva." : "La sucursal ya está activa.",
                BranchErrorKind.Conflict);
        }

        repository.AddAuditEvent(CreateAudit(
            deactivate ? "BRANCH_DEACTIVATED" : "BRANCH_REACTIVATED",
            actor,
            organizationId,
            null,
            client,
            now,
            branch.Id));
        await repository.SaveChangesAsync(cancellationToken);
        return await GetBranchAsync(id, actor, cancellationToken);
    }

    private async Task<BranchAdministratorResult> ChangeAdministratorStatusAsync(
        Guid id,
        bool suspend,
        CurrentAccount actor,
        ClientContext client,
        CancellationToken cancellationToken)
    {
        var organizationId = EnsureSuperAdministrator(actor);
        var account = await repository.FindAdministratorAsync(organizationId, id, cancellationToken)
            ?? throw AdministratorNotFound();
        var now = clock.UtcNow;
        if (!suspend)
        {
            var accessIds = (await repository.FindActiveAccessesAsync(organizationId, id, cancellationToken))
                .Select(item => item.BranchId)
                .Distinct()
                .ToArray();
            await ValidateBranchIdsAsync(organizationId, accessIds, cancellationToken);
        }

        var changed = suspend
            ? account.Suspend(secureTokenService.GenerateSecurityStamp(), now)
            : account.Reactivate(secureTokenService.GenerateSecurityStamp(), now);
        if (!changed)
        {
            throw new BranchException(
                BranchErrorCodes.InvalidAdministratorStatus,
                suspend ? "El administrador ya está suspendido." : "El administrador ya está activo.",
                BranchErrorKind.Conflict);
        }

        if (suspend)
        {
            foreach (var session in await repository.FindActiveSessionsAsync(account.Id, now, cancellationToken))
            {
                session.Revoke(now, client.IpAddress, "ACCOUNT_SUSPENDED");
            }

            foreach (var invitation in await repository.FindActiveInvitationsAsync(account.Id, cancellationToken))
            {
                invitation.Revoke(now);
            }
        }

        repository.AddAuditEvent(CreateAudit(
            suspend ? "BRANCH_ADMINISTRATOR_SUSPENDED" : "BRANCH_ADMINISTRATOR_REACTIVATED",
            actor,
            organizationId,
            account.Id,
            client,
            now));
        await repository.SaveChangesAsync(cancellationToken);
        return await GetAdministratorAsync(id, actor, cancellationToken);
    }

    private async Task<ValidatedBranch> ValidateBranchAsync(
        BranchInput input,
        Guid organizationId,
        Guid? existingBranchId,
        CancellationToken cancellationToken)
    {
        var name = CleanRequired(input.Name, 150, "nombre");
        var normalizedName = name.ToUpperInvariant();
        if (await repository.BranchNameExistsAsync(
            organizationId,
            normalizedName,
            existingBranchId,
            cancellationToken))
        {
            throw new BranchException(
                BranchErrorCodes.DuplicateName,
                "Ya existe una sucursal con ese nombre en la organización.",
                BranchErrorKind.Conflict);
        }

        var municipalityCode = CleanRequired(input.MunicipalityCode, 5, "municipio");
        if (municipalityCode.Length != 5 ||
            !municipalityCode.All(char.IsAsciiDigit) ||
            await repository.FindMunicipalityAsync(municipalityCode, cancellationToken) is null)
        {
            throw new BranchException(
                BranchErrorCodes.InvalidMunicipality,
                "El municipio no pertenece al catálogo DIVIPOLA vigente.");
        }

        return new ValidatedBranch(
            name,
            normalizedName,
            ValidateOptionalEmail(input.ContactEmail),
            NormalizeOptionalPhone(input.Phone),
            CleanRequired(input.Address, 250, "dirección"),
            municipalityCode);
    }

    private ValidatedAdministrator ValidateAdministrator(BranchAdministratorInput input)
    {
        var email = input.Email?.Trim() ?? string.Empty;
        if (email.Length is 0 or > 320 ||
            !MailAddress.TryCreate(email, out var parsed) ||
            !string.Equals(parsed.Address, email, StringComparison.OrdinalIgnoreCase))
        {
            throw new BranchException(BranchErrorCodes.InvalidData, "El correo del administrador no es válido.");
        }

        return new ValidatedAdministrator(
            CleanRequired(input.FirstName, 100, "nombre del administrador"),
            CleanRequired(input.LastName, 100, "apellido del administrador"),
            email,
            emailNormalizer.Normalize(email));
    }

    private async Task<IReadOnlyCollection<Branch>> ValidateBranchIdsAsync(
        Guid organizationId,
        IReadOnlyCollection<Guid>? branchIds,
        CancellationToken cancellationToken)
    {
        var ids = (branchIds ?? []).Where(item => item != Guid.Empty).Distinct().ToArray();
        if (ids.Length == 0)
        {
            throw new BranchException(
                BranchErrorCodes.BranchAccessRequired,
                "Selecciona al menos una sucursal activa.");
        }

        var branches = await repository.FindActiveBranchesAsync(organizationId, ids, cancellationToken);
        if (branches.Count != ids.Length)
        {
            throw new BranchException(
                BranchErrorCodes.InvalidBranchAccess,
                "Una o más sucursales no existen, están inactivas o pertenecen a otra organización.");
        }

        return branches;
    }

    private async Task ReplaceAccessesAsync(
        Guid organizationId,
        Guid accountId,
        IReadOnlyCollection<Branch> branches,
        Guid actorId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var requestedIds = branches.Select(item => item.Id).ToHashSet();
        var current = await repository.FindActiveAccessesAsync(organizationId, accountId, cancellationToken);
        foreach (var access in current.Where(item => !requestedIds.Contains(item.BranchId)))
        {
            access.Revoke(actorId, now);
        }

        var currentIds = current.Select(item => item.BranchId).ToHashSet();
        foreach (var branch in branches.Where(item => !currentIds.Contains(item.Id)))
        {
            repository.AddAccess(UserBranchAccess.Grant(
                organizationId,
                accountId,
                branch.Id,
                actorId,
                now));
        }
    }

    private BranchAdministratorResult ToAdministratorResult(BranchAdministratorQueryItem item) => new(
        item.Account.Id,
        item.Account.FirstName,
        item.Account.LastName,
        item.Account.Email,
        item.Account.Status == AccountStatus.Active ? AccountStatuses.Active : AccountStatuses.Suspended,
        tenantInvitations.GetPublicStatus(item.Account, item.Invitation, clock.UtcNow),
        item.Account.EmailVerifiedAt is null && item.Invitation?.RevokedAt is null
            ? item.Invitation?.ExpiresAt
            : null,
        item.Branches.Select(branch => new BranchAssignmentResult(
            branch.Id,
            branch.Name,
            branch.Status == BranchStatus.Active ? BranchStatuses.Active : BranchStatuses.Inactive)).ToArray(),
        item.Account.CreatedAt,
        item.Account.UpdatedAt);

    private static BranchResult ToBranchResult(BranchQueryItem item) => new(
        item.Branch.Id,
        item.Branch.Name,
        item.Branch.ContactEmail,
        item.Branch.Phone,
        item.Branch.Address,
        item.Branch.MunicipalityCode,
        item.Municipality.Name,
        item.Department.Code,
        item.Department.Name,
        item.Branch.Status == BranchStatus.Active ? BranchStatuses.Active : BranchStatuses.Inactive,
        item.Branch.CreatedAt,
        item.Branch.UpdatedAt);

    private static Guid EnsureTenantActor(CurrentAccount actor)
    {
        if (actor.AccountType != AccountType.Tenant || actor.OrganizationId is not { } organizationId)
        {
            throw new BranchException(
                BranchErrorCodes.Forbidden,
                "La cuenta no pertenece a una organización.",
                BranchErrorKind.Forbidden);
        }

        if (!IsSuperAdministrator(actor) && !actor.Roles.Contains(SystemRoleCodes.BranchAdmin))
        {
            throw new BranchException(
                BranchErrorCodes.Forbidden,
                "La cuenta no tiene acceso a sucursales.",
                BranchErrorKind.Forbidden);
        }

        return organizationId;
    }

    private static Guid EnsureSuperAdministrator(CurrentAccount actor)
    {
        var organizationId = EnsureTenantActor(actor);
        if (!IsSuperAdministrator(actor))
        {
            throw new BranchException(
                BranchErrorCodes.Forbidden,
                "Solo un superadministrador puede realizar esta acción.",
                BranchErrorKind.Forbidden);
        }

        return organizationId;
    }

    private static bool IsSuperAdministrator(CurrentAccount actor) =>
        actor.Roles.Contains(SystemRoleCodes.SuperAdmin);

    private static void EnsurePending(UserAccount account)
    {
        if (account.EmailVerifiedAt is not null)
        {
            throw new BranchException(
                BranchErrorCodes.AdministratorAlreadyAccepted,
                "El administrador ya activó su cuenta.",
                BranchErrorKind.Conflict);
        }
    }

    private static void EnsureActive(UserAccount account)
    {
        if (account.Status != AccountStatus.Active)
        {
            throw new BranchException(
                BranchErrorCodes.InvalidAdministratorStatus,
                "Reactiva la cuenta antes de enviar una invitación.",
                BranchErrorKind.Conflict);
        }
    }

    private static void ValidatePagination(int page, int pageSize)
    {
        if (page < 1 || pageSize is < 1 or > 100)
        {
            throw new BranchException(
                BranchErrorCodes.InvalidData,
                "La página debe ser mayor que cero y pageSize debe estar entre 1 y 100.");
        }
    }

    private static BranchStatus? ParseBranchStatus(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim().ToUpperInvariant() switch
            {
                BranchStatuses.Active => BranchStatus.Active,
                BranchStatuses.Inactive => BranchStatus.Inactive,
                _ => throw new BranchException(BranchErrorCodes.InvalidData, "El estado debe ser ACTIVE o INACTIVE.")
            };

    private static AccountStatus? ParseAccountStatus(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim().ToUpperInvariant() switch
            {
                AccountStatuses.Active => AccountStatus.Active,
                AccountStatuses.Suspended => AccountStatus.Suspended,
                _ => throw new BranchException(BranchErrorCodes.InvalidData, "El estado debe ser ACTIVE o SUSPENDED.")
            };

    private static string CleanRequired(string? value, int maximumLength, string field)
    {
        var cleaned = string.Join(' ', (value ?? string.Empty).Split(
            (char[]?)null,
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        if (cleaned.Length is 0 || cleaned.Length > maximumLength)
        {
            throw new BranchException(
                BranchErrorCodes.InvalidData,
                $"El campo {field} es obligatorio y admite máximo {maximumLength} caracteres.");
        }

        return cleaned;
    }

    private static string? CleanOptional(string? value, int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var cleaned = value.Trim();
        if (cleaned.Length > maximumLength)
        {
            throw new BranchException(BranchErrorCodes.InvalidData, $"La búsqueda admite máximo {maximumLength} caracteres.");
        }

        return cleaned;
    }

    private static string? ValidateOptionalEmail(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var email = value.Trim();
        if (email.Length > 320 ||
            !MailAddress.TryCreate(email, out var parsed) ||
            !string.Equals(parsed.Address, email, StringComparison.OrdinalIgnoreCase))
        {
            throw new BranchException(BranchErrorCodes.InvalidData, "El correo de contacto no es válido.");
        }

        return email;
    }

    private static string? NormalizeOptionalPhone(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var phone = new string(value.Where(character => character is not (' ' or '-' or '(' or ')')).ToArray());
        var digits = phone.StartsWith('+') ? phone[1..] : phone;
        if (digits.Length is < 7 or > 15 || !digits.All(char.IsAsciiDigit))
        {
            throw new BranchException(
                BranchErrorCodes.InvalidData,
                "El teléfono debe contener entre 7 y 15 dígitos y puede comenzar con +.");
        }

        return phone;
    }

    private static BranchException BranchNotFound() => new(
        BranchErrorCodes.NotFound,
        "La sucursal no existe.",
        BranchErrorKind.NotFound);

    private static BranchException AdministratorNotFound() => new(
        BranchErrorCodes.AdministratorNotFound,
        "El administrador no existe.",
        BranchErrorKind.NotFound);

    private static BranchException DuplicateEmail() => new(
        BranchErrorCodes.DuplicateAccountEmail,
        "El correo ya pertenece a otra cuenta.",
        BranchErrorKind.Conflict);

    private static SecurityAuditEvent CreateAudit(
        string eventType,
        CurrentAccount actor,
        Guid organizationId,
        Guid? affectedAccountId,
        ClientContext client,
        DateTimeOffset now,
        Guid? branchId = null) =>
        SecurityAuditEvent.Create(
            eventType,
            "SUCCESS",
            now,
            AccountType.Tenant,
            userAccountId: affectedAccountId,
            ipAddress: client.IpAddress,
            userAgent: client.UserAgent,
            organizationId: organizationId,
            actorUserAccountId: actor.UserId,
            branchId: branchId);

    private sealed record ValidatedBranch(
        string Name,
        string NormalizedName,
        string? ContactEmail,
        string? Phone,
        string Address,
        string MunicipalityCode);

    private sealed record ValidatedAdministrator(
        string FirstName,
        string LastName,
        string Email,
        string NormalizedEmail);
}
