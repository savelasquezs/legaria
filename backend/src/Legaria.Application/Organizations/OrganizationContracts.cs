using Legaria.Application.Authentication;

namespace Legaria.Application.Organizations;

public sealed record InitialAdministratorInput(string FirstName, string LastName, string Email);

public sealed record OrganizationInput(
    string TradeName,
    string LegalName,
    string Nit,
    int VerificationDigit,
    string ContactEmail,
    string Phone,
    string Address,
    string MunicipalityCode);

public sealed record CreateOrganizationRequest(
    string TradeName,
    string LegalName,
    string Nit,
    int VerificationDigit,
    string ContactEmail,
    string Phone,
    string Address,
    string MunicipalityCode,
    InitialAdministratorInput InitialAdmin);

public sealed record UpdateOrganizationRequest(
    string TradeName,
    string LegalName,
    string Nit,
    int VerificationDigit,
    string ContactEmail,
    string Phone,
    string Address,
    string MunicipalityCode);

public sealed record AcceptInvitationRequest(string Token, string NewPassword);

public sealed record DepartmentResult(string Code, string Name);
public sealed record MunicipalityResult(string Code, string Name, string Type);

public sealed record InitialAdministratorResult(
    Guid Id,
    string FirstName,
    string LastName,
    string Email,
    string InvitationStatus,
    DateTimeOffset? InvitationExpiresAt);

public sealed record OrganizationResult(
    Guid Id,
    string TradeName,
    string LegalName,
    string Nit,
    int VerificationDigit,
    string ContactEmail,
    string Phone,
    string Address,
    string MunicipalityCode,
    string MunicipalityName,
    string DepartmentCode,
    string DepartmentName,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    InitialAdministratorResult InitialAdmin);

public sealed record OrganizationListItem(
    Guid Id,
    string TradeName,
    string LegalName,
    string Nit,
    int VerificationDigit,
    string MunicipalityName,
    string DepartmentName,
    string Status,
    string InvitationStatus,
    DateTimeOffset CreatedAt);

public sealed record OrganizationPage(
    IReadOnlyCollection<OrganizationListItem> Items,
    int Page,
    int PageSize,
    int TotalItems,
    int TotalPages);

public static class InvitationStatuses
{
    public const string PendingDelivery = "PENDING_DELIVERY";
    public const string Sent = "SENT";
    public const string DeliveryFailed = "DELIVERY_FAILED";
    public const string Expired = "EXPIRED";
    public const string Accepted = "ACCEPTED";
    public const string Revoked = "REVOKED";
}

public static class OrganizationErrorCodes
{
    public const string NotFound = "organization.not_found";
    public const string InvalidNit = "organization.invalid_nit";
    public const string DuplicateNit = "organization.duplicate_nit";
    public const string InvalidMunicipality = "organization.invalid_municipality";
    public const string InvalidStatusTransition = "organization.invalid_status_transition";
    public const string InvalidData = "organization.invalid_data";
    public const string DuplicateAccountEmail = "account.duplicate_email";
    public const string InitialAdminAlreadyAccepted = "organization.initial_admin_already_accepted";
    public const string InvalidInvitation = "invitation.invalid";
    public const string ExpiredInvitation = "invitation.expired";
    public const string UsedInvitation = "invitation.used";
    public const string SuspendedOrganization = "invitation.organization_suspended";
}

public enum OrganizationErrorKind
{
    Validation,
    NotFound,
    Conflict,
    Forbidden
}

public sealed class OrganizationException(
    string code,
    string message,
    OrganizationErrorKind kind = OrganizationErrorKind.Validation) : Exception(message)
{
    public string Code { get; } = code;
    public OrganizationErrorKind Kind { get; } = kind;
}

public interface IOrganizationService
{
    Task<OrganizationPage> ListAsync(int page, int pageSize, string? search, string? status, CancellationToken cancellationToken);
    Task<OrganizationResult> GetAsync(Guid id, CancellationToken cancellationToken);
    Task<OrganizationResult> CreateAsync(CreateOrganizationRequest request, CurrentAccount actor, ClientContext client, CancellationToken cancellationToken);
    Task<OrganizationResult> UpdateAsync(Guid id, UpdateOrganizationRequest request, CurrentAccount actor, ClientContext client, CancellationToken cancellationToken);
    Task<OrganizationResult> SuspendAsync(Guid id, CurrentAccount actor, ClientContext client, CancellationToken cancellationToken);
    Task<OrganizationResult> ReactivateAsync(Guid id, CurrentAccount actor, ClientContext client, CancellationToken cancellationToken);
    Task<OrganizationResult> UpdateInitialAdminAsync(Guid id, InitialAdministratorInput request, CurrentAccount actor, ClientContext client, CancellationToken cancellationToken);
    Task<OrganizationResult> ResendInvitationAsync(Guid id, CurrentAccount actor, ClientContext client, CancellationToken cancellationToken);
    Task AcceptInvitationAsync(AcceptInvitationRequest request, ClientContext client, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<DepartmentResult>> GetDepartmentsAsync(CancellationToken cancellationToken);
    Task<IReadOnlyCollection<MunicipalityResult>> GetMunicipalitiesAsync(string departmentCode, CancellationToken cancellationToken);
}
