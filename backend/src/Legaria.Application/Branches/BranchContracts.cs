using Legaria.Application.Organizations;

namespace Legaria.Application.Branches;

public sealed record BranchInput(
    string Name,
    string? ContactEmail,
    string? Phone,
    string Address,
    string MunicipalityCode);

public sealed record BranchResult(
    Guid Id,
    string Name,
    string? ContactEmail,
    string? Phone,
    string Address,
    string MunicipalityCode,
    string MunicipalityName,
    string DepartmentCode,
    string DepartmentName,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record BranchPage(
    IReadOnlyCollection<BranchResult> Items,
    int Page,
    int PageSize,
    int TotalItems,
    int TotalPages);

public sealed record BranchAssignmentResult(Guid Id, string Name, string Status);

public sealed record BranchAdministratorInput(
    string FirstName,
    string LastName,
    string Email,
    IReadOnlyCollection<Guid> BranchIds);

public sealed record UpdateBranchAssignmentsRequest(IReadOnlyCollection<Guid> BranchIds);

public sealed record BranchAdministratorResult(
    Guid Id,
    string FirstName,
    string LastName,
    string Email,
    string AccountStatus,
    string InvitationStatus,
    DateTimeOffset? InvitationExpiresAt,
    IReadOnlyCollection<BranchAssignmentResult> Branches,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record BranchAdministratorPage(
    IReadOnlyCollection<BranchAdministratorResult> Items,
    int Page,
    int PageSize,
    int TotalItems,
    int TotalPages);

public static class BranchStatuses
{
    public const string Active = "ACTIVE";
    public const string Inactive = "INACTIVE";
}

public static class AccountStatuses
{
    public const string Active = "ACTIVE";
    public const string Suspended = "SUSPENDED";
}

public static class BranchErrorCodes
{
    public const string NotFound = "branch.not_found";
    public const string InvalidData = "branch.invalid_data";
    public const string DuplicateName = "branch.duplicate_name";
    public const string InitialBranchAlreadyExists = "branch.initial_already_exists";
    public const string InvalidMunicipality = "branch.invalid_municipality";
    public const string InvalidStatusTransition = "branch.invalid_status_transition";
    public const string Forbidden = "branch.forbidden";
    public const string AdministratorNotFound = "branch_administrator.not_found";
    public const string AdministratorAlreadyAccepted = "branch_administrator.already_accepted";
    public const string InvalidAdministratorStatus = "branch_administrator.invalid_status_transition";
    public const string BranchAccessRequired = "branch_access.required";
    public const string InvalidBranchAccess = "branch_access.invalid";
    public const string DuplicateAccountEmail = OrganizationErrorCodes.DuplicateAccountEmail;
}

public enum BranchErrorKind
{
    Validation,
    NotFound,
    Conflict,
    Forbidden
}

public sealed class BranchException(
    string code,
    string message,
    BranchErrorKind kind = BranchErrorKind.Validation) : Exception(message)
{
    public string Code { get; } = code;
    public BranchErrorKind Kind { get; } = kind;
}
