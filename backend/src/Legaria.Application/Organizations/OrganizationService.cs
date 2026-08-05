using System.Net.Mail;
using Legaria.Application.Authentication;
using Legaria.Domain.Authentication;
using Legaria.Domain.Tenancy;

namespace Legaria.Application.Organizations;

public sealed class OrganizationService(
    IOrganizationRepository repository,
    INitValidator nitValidator,
    IEmailNormalizer emailNormalizer,
    IPasswordService passwordService,
    ISecureTokenService secureTokenService,
    IClock clock,
    ITenantInvitationService tenantInvitations) : IOrganizationService
{
    public async Task<OrganizationPage> ListAsync(
        int page,
        int pageSize,
        string? search,
        string? status,
        CancellationToken cancellationToken)
    {
        if (page < 1 || pageSize is < 1 or > 100)
        {
            throw InvalidData("La página debe ser mayor que cero y pageSize debe estar entre 1 y 100.");
        }

        var parsedStatus = ParseStatus(status);
        var (items, total) = await repository.ListAsync(
            (page - 1) * pageSize,
            pageSize,
            CleanOptional(search, 200),
            parsedStatus,
            clock.UtcNow,
            cancellationToken);

        return new OrganizationPage(
            items.Select(ToListItem).ToArray(),
            page,
            pageSize,
            total,
            total == 0 ? 0 : (int)Math.Ceiling(total / (double)pageSize));
    }

    public async Task<OrganizationResult> GetAsync(Guid id, CancellationToken cancellationToken) =>
        ToResult(await GetDetailsAsync(id, cancellationToken), clock.UtcNow);

    public async Task<OrganizationResult> CreateAsync(
        CreateOrganizationRequest request,
        CurrentAccount actor,
        ClientContext client,
        CancellationToken cancellationToken)
    {
        EnsurePlatformActor(actor);
        var organizationInput = await ValidateOrganizationAsync(
            new OrganizationInput(
                request.TradeName,
                request.LegalName,
                request.Nit,
                request.VerificationDigit,
                request.ContactEmail,
                request.Phone,
                request.Address,
                request.MunicipalityCode),
            null,
            cancellationToken);
        var admin = ValidateAdministrator(request.InitialAdmin);
        if (await repository.EmailExistsAsync(admin.NormalizedEmail, null, cancellationToken))
        {
            throw DuplicateEmail();
        }

        var now = clock.UtcNow;
        var organization = Organization.Create(
            organizationInput.TradeName,
            organizationInput.LegalName,
            organizationInput.Nit,
            organizationInput.VerificationDigit,
            organizationInput.ContactEmail,
            organizationInput.Phone,
            organizationInput.Address,
            organizationInput.MunicipalityCode,
            now);
        var account = UserAccount.Create(
            organization.Id,
            null,
            admin.Email,
            admin.NormalizedEmail,
            passwordService.Hash(secureTokenService.GenerateToken()),
            admin.FirstName,
            admin.LastName,
            secureTokenService.GenerateSecurityStamp(),
            false,
            now,
            true);
        account.AddRole(SystemRole.SuperAdminId);
        var invitation = await tenantInvitations.IssueAsync(
            account.Id,
            client,
            false,
            cancellationToken);

        repository.AddOrganization(organization);
        repository.AddUserAccount(account);
        repository.AddAccountEmail(AccountEmail.ForTenant(admin.NormalizedEmail, account.Id, now));
        repository.AddAuditEvent(CreateAudit(
            "ORGANIZATION_CREATED",
            actor,
            organization.Id,
            account.Id,
            client,
            now));
        await repository.SaveChangesAsync(cancellationToken);

        await tenantInvitations.DeliverAsync(
            organization,
            account,
            invitation,
            "superadministrador inicial",
            actor,
            client,
            cancellationToken);
        return await GetAsync(organization.Id, cancellationToken);
    }

    public async Task<OrganizationResult> UpdateAsync(
        Guid id,
        UpdateOrganizationRequest request,
        CurrentAccount actor,
        ClientContext client,
        CancellationToken cancellationToken)
    {
        EnsurePlatformActor(actor);
        var organization = await repository.FindOrganizationAsync(id, cancellationToken)
            ?? throw NotFound();
        var input = await ValidateOrganizationAsync(
            new OrganizationInput(
                request.TradeName,
                request.LegalName,
                request.Nit,
                request.VerificationDigit,
                request.ContactEmail,
                request.Phone,
                request.Address,
                request.MunicipalityCode),
            id,
            cancellationToken);
        var now = clock.UtcNow;
        organization.Update(
            input.TradeName,
            input.LegalName,
            input.Nit,
            input.VerificationDigit,
            input.ContactEmail,
            input.Phone,
            input.Address,
            input.MunicipalityCode,
            now);
        repository.AddAuditEvent(CreateAudit("ORGANIZATION_UPDATED", actor, id, null, client, now));
        await repository.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    public Task<OrganizationResult> SuspendAsync(
        Guid id,
        CurrentAccount actor,
        ClientContext client,
        CancellationToken cancellationToken) =>
        ChangeStatusAsync(id, true, actor, client, cancellationToken);

    public Task<OrganizationResult> ReactivateAsync(
        Guid id,
        CurrentAccount actor,
        ClientContext client,
        CancellationToken cancellationToken) =>
        ChangeStatusAsync(id, false, actor, client, cancellationToken);

    public async Task<OrganizationResult> UpdateInitialAdminAsync(
        Guid id,
        InitialAdministratorInput request,
        CurrentAccount actor,
        ClientContext client,
        CancellationToken cancellationToken)
    {
        EnsurePlatformActor(actor);
        var organization = await repository.FindOrganizationAsync(id, cancellationToken)
            ?? throw NotFound();
        var account = await repository.FindInitialAdminAsync(id, cancellationToken)
            ?? throw NotFound();
        EnsureInvitationPending(account);
        var admin = ValidateAdministrator(request);

        if (await repository.EmailExistsAsync(admin.NormalizedEmail, account.Id, cancellationToken))
        {
            throw DuplicateEmail();
        }

        var now = clock.UtcNow;
        if (!string.Equals(account.NormalizedEmail, admin.NormalizedEmail, StringComparison.Ordinal))
        {
            var reservation = await repository.FindAccountEmailForUserAsync(account.Id, cancellationToken)
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
        var invitation = await tenantInvitations.IssueAsync(
            account.Id,
            client,
            true,
            cancellationToken);
        repository.AddAuditEvent(CreateAudit("INITIAL_ADMIN_UPDATED", actor, id, account.Id, client, now));
        await repository.SaveChangesAsync(cancellationToken);

        await tenantInvitations.DeliverAsync(
            organization,
            account,
            invitation,
            "superadministrador inicial",
            actor,
            client,
            cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    public async Task<OrganizationResult> ResendInvitationAsync(
        Guid id,
        CurrentAccount actor,
        ClientContext client,
        CancellationToken cancellationToken)
    {
        EnsurePlatformActor(actor);
        var organization = await repository.FindOrganizationAsync(id, cancellationToken)
            ?? throw NotFound();
        var account = await repository.FindInitialAdminAsync(id, cancellationToken)
            ?? throw NotFound();
        EnsureInvitationPending(account);
        var now = clock.UtcNow;
        var invitation = await tenantInvitations.IssueAsync(
            account.Id,
            client,
            true,
            cancellationToken);
        repository.AddAuditEvent(CreateAudit("TENANT_INVITATION_REISSUED", actor, id, account.Id, client, now));
        await repository.SaveChangesAsync(cancellationToken);

        await tenantInvitations.DeliverAsync(
            organization,
            account,
            invitation,
            "superadministrador inicial",
            actor,
            client,
            cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    public Task AcceptInvitationAsync(
        AcceptInvitationRequest request,
        ClientContext client,
        CancellationToken cancellationToken) =>
        tenantInvitations.AcceptAsync(request, client, cancellationToken);

    public async Task<IReadOnlyCollection<DepartmentResult>> GetDepartmentsAsync(CancellationToken cancellationToken) =>
        (await repository.GetDepartmentsAsync(cancellationToken))
            .Select(item => new DepartmentResult(item.Code, item.Name))
            .ToArray();

    public async Task<IReadOnlyCollection<MunicipalityResult>> GetMunicipalitiesAsync(
        string departmentCode,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(departmentCode) || departmentCode.Length != 2 || !departmentCode.All(char.IsAsciiDigit))
        {
            throw new OrganizationException(
                OrganizationErrorCodes.InvalidMunicipality,
                "El código de departamento no es válido.");
        }

        return (await repository.GetMunicipalitiesAsync(departmentCode, cancellationToken))
            .Select(item => new MunicipalityResult(item.Code, item.Name, item.Type))
            .ToArray();
    }

    private async Task<OrganizationResult> ChangeStatusAsync(
        Guid id,
        bool suspend,
        CurrentAccount actor,
        ClientContext client,
        CancellationToken cancellationToken)
    {
        EnsurePlatformActor(actor);
        var organization = await repository.FindOrganizationAsync(id, cancellationToken)
            ?? throw NotFound();
        var now = clock.UtcNow;
        var changed = suspend ? organization.Suspend(now) : organization.Reactivate(now);
        if (!changed)
        {
            throw new OrganizationException(
                OrganizationErrorCodes.InvalidStatusTransition,
                suspend ? "La organización ya está suspendida." : "La organización ya está activa.",
                OrganizationErrorKind.Conflict);
        }

        repository.AddAuditEvent(CreateAudit(
            suspend ? "ORGANIZATION_SUSPENDED" : "ORGANIZATION_REACTIVATED",
            actor,
            id,
            null,
            client,
            now));
        await repository.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    private async Task<OrganizationInput> ValidateOrganizationAsync(
        OrganizationInput input,
        Guid? existingOrganizationId,
        CancellationToken cancellationToken)
    {
        var result = new OrganizationInput(
            CleanRequired(input.TradeName, 200, "nombre comercial"),
            CleanRequired(input.LegalName, 200, "razón social"),
            CleanRequired(input.Nit, 14, "NIT"),
            input.VerificationDigit,
            ValidateEmail(input.ContactEmail, "correo de contacto").Email,
            NormalizePhone(input.Phone),
            CleanRequired(input.Address, 250, "dirección"),
            CleanRequired(input.MunicipalityCode, 5, "municipio"));

        if (!nitValidator.IsValid(result.Nit, result.VerificationDigit))
        {
            throw new OrganizationException(
                OrganizationErrorCodes.InvalidNit,
                "El NIT o su dígito de verificación no son válidos.");
        }

        if (await repository.NitExistsAsync(result.Nit, existingOrganizationId, cancellationToken))
        {
            throw new OrganizationException(
                OrganizationErrorCodes.DuplicateNit,
                "Ya existe una organización con ese NIT.",
                OrganizationErrorKind.Conflict);
        }

        if (result.MunicipalityCode.Length != 5 ||
            !result.MunicipalityCode.All(char.IsAsciiDigit) ||
            await repository.FindMunicipalityAsync(result.MunicipalityCode, cancellationToken) is null)
        {
            throw new OrganizationException(
                OrganizationErrorCodes.InvalidMunicipality,
                "El municipio no pertenece al catálogo DIVIPOLA vigente.");
        }

        return result;
    }

    private ValidatedAdministrator ValidateAdministrator(InitialAdministratorInput input)
    {
        var validatedEmail = ValidateEmail(input.Email, "correo del superadministrador");
        return new ValidatedAdministrator(
            CleanRequired(input.FirstName, 100, "nombre del superadministrador"),
            CleanRequired(input.LastName, 100, "apellido del superadministrador"),
            validatedEmail.Email,
            validatedEmail.NormalizedEmail);
    }

    private (string Email, string NormalizedEmail) ValidateEmail(string value, string field)
    {
        var email = value?.Trim() ?? string.Empty;
        if (email.Length > 320 || !MailAddress.TryCreate(email, out var parsed) ||
            !string.Equals(parsed.Address, email, StringComparison.OrdinalIgnoreCase))
        {
            throw InvalidData($"El {field} no es válido.");
        }

        return (email, emailNormalizer.Normalize(email));
    }

    private static string NormalizePhone(string value)
    {
        var phone = new string((value ?? string.Empty).Where(character => character is not (' ' or '-' or '(' or ')')).ToArray());
        var digits = phone.StartsWith('+') ? phone[1..] : phone;
        if (digits.Length is < 7 or > 15 || !digits.All(char.IsAsciiDigit))
        {
            throw InvalidData("El teléfono debe contener entre 7 y 15 dígitos y puede comenzar con +.");
        }

        return phone;
    }

    private async Task<OrganizationQueryItem> GetDetailsAsync(Guid id, CancellationToken cancellationToken) =>
        await repository.FindDetailsAsync(id, cancellationToken) ?? throw NotFound();

    private OrganizationResult ToResult(OrganizationQueryItem item, DateTimeOffset now) => new(
        item.Organization.Id,
        item.Organization.TradeName,
        item.Organization.LegalName,
        item.Organization.Nit,
        item.Organization.VerificationDigit,
        item.Organization.ContactEmail,
        item.Organization.Phone,
        item.Organization.Address,
        item.Organization.MunicipalityCode,
        item.Municipality.Name,
        item.Department.Code,
        item.Department.Name,
        ToStatus(item.Organization.Status),
        item.Organization.CreatedAt,
        item.Organization.UpdatedAt,
        new InitialAdministratorResult(
            item.InitialAdmin.Id,
            item.InitialAdmin.FirstName,
            item.InitialAdmin.LastName,
            item.InitialAdmin.Email,
            tenantInvitations.GetPublicStatus(item.InitialAdmin, item.Invitation, now),
            item.InitialAdmin.EmailVerifiedAt is null ? item.Invitation?.ExpiresAt : null));

    private OrganizationListItem ToListItem(OrganizationQueryItem item) => new(
        item.Organization.Id,
        item.Organization.TradeName,
        item.Organization.LegalName,
        item.Organization.Nit,
        item.Organization.VerificationDigit,
        item.Municipality.Name,
        item.Department.Name,
        ToStatus(item.Organization.Status),
        tenantInvitations.GetPublicStatus(item.InitialAdmin, item.Invitation, clock.UtcNow),
        item.Organization.CreatedAt);

    private static void EnsureInvitationPending(UserAccount account)
    {
        if (account.EmailVerifiedAt is not null)
        {
            throw new OrganizationException(
                OrganizationErrorCodes.InitialAdminAlreadyAccepted,
                "El superadministrador inicial ya activó su cuenta.",
                OrganizationErrorKind.Conflict);
        }
    }

    private static void EnsurePlatformActor(CurrentAccount actor)
    {
        if (actor.AccountType != AccountType.Platform)
        {
            throw new OrganizationException(
                AuthErrorCodes.AccountUnavailable,
                "La cuenta no puede administrar organizaciones.",
                OrganizationErrorKind.Forbidden);
        }
    }

    private static OrganizationStatus? ParseStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return null;
        }

        return status.Trim().ToUpperInvariant() switch
        {
            "ACTIVE" => OrganizationStatus.Active,
            "SUSPENDED" => OrganizationStatus.Suspended,
            _ => throw InvalidData("El estado debe ser ACTIVE o SUSPENDED.")
        };
    }

    private static string ToStatus(OrganizationStatus status) =>
        status == OrganizationStatus.Active ? "ACTIVE" : "SUSPENDED";

    private static string CleanRequired(string? value, int maximumLength, string field)
    {
        var cleaned = string.Join(' ', (value ?? string.Empty).Split(
            (char[]?)null,
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        if (cleaned.Length is 0 || cleaned.Length > maximumLength)
        {
            throw InvalidData($"El campo {field} es obligatorio y admite máximo {maximumLength} caracteres.");
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
            throw InvalidData($"La búsqueda admite máximo {maximumLength} caracteres.");
        }

        return cleaned;
    }

    private static OrganizationException NotFound() => new(
        OrganizationErrorCodes.NotFound,
        "La organización no existe.",
        OrganizationErrorKind.NotFound);

    private static OrganizationException InvalidData(string message) => new(
        OrganizationErrorCodes.InvalidData,
        message);

    private static OrganizationException DuplicateEmail() => new(
        OrganizationErrorCodes.DuplicateAccountEmail,
        "El correo ya pertenece a otra cuenta.",
        OrganizationErrorKind.Conflict);

    private static SecurityAuditEvent CreateAudit(
        string eventType,
        CurrentAccount actor,
        Guid organizationId,
        Guid? affectedAccountId,
        ClientContext client,
        DateTimeOffset now,
        string outcome = "SUCCESS") =>
        SecurityAuditEvent.Create(
            eventType,
            outcome,
            now,
            AccountType.Platform,
            platformUserId: actor.UserId,
            userAccountId: affectedAccountId,
            ipAddress: client.IpAddress,
            userAgent: client.UserAgent,
            organizationId: organizationId);

    private sealed record ValidatedAdministrator(
        string FirstName,
        string LastName,
        string Email,
        string NormalizedEmail);
}
