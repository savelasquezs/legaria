namespace Legaria.Application.Employees;

public sealed record AdministrativeAccessInput(
    string? Email,
    IReadOnlyCollection<Guid> BranchIds);

public sealed record CreateEmployeeInput(
    string DocumentType,
    string DocumentNumber,
    string FirstName,
    string LastName,
    Guid JobPositionId,
    DateOnly StartedOn,
    bool IsPrimary,
    AdministrativeAccessInput? AdministrativeAccess);

public sealed record AssignEmployeeInput(
    Guid JobPositionId,
    DateOnly StartedOn,
    bool IsPrimary,
    AdministrativeAccessInput? AdministrativeAccess);

public sealed record EmployeeAssignmentResult(
    Guid Id,
    Guid EmploymentRelationshipId,
    Guid BranchId,
    string BranchName,
    Guid JobPositionId,
    string JobPositionName,
    bool IsPrimary,
    DateOnly StartedOn,
    DateOnly? EndedOn,
    string Status);

public sealed record EmploymentRelationshipResult(
    Guid Id,
    DateOnly StartedOn,
    DateOnly? EndedOn,
    string Status,
    IReadOnlyCollection<EmployeeAssignmentResult> Assignments);

public sealed record EmployeeAdministrativeAccessResult(
    Guid AccountId,
    string Email,
    string AccountStatus,
    string InvitationStatus,
    DateTimeOffset? InvitationExpiresAt,
    IReadOnlyCollection<Guid> BranchIds);

public sealed record EmployeeResult(
    Guid Id,
    string DocumentType,
    string DocumentNumber,
    string FirstName,
    string LastName,
    IReadOnlyCollection<EmployeeAssignmentResult> Assignments,
    EmployeeAdministrativeAccessResult? AdministrativeAccess,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record EmployeeDetailResult(
    Guid Id,
    string DocumentType,
    string DocumentNumber,
    string FirstName,
    string LastName,
    IReadOnlyCollection<EmploymentRelationshipResult> EmploymentRelationships,
    EmployeeAdministrativeAccessResult? AdministrativeAccess,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record EmployeePage(
    IReadOnlyCollection<EmployeeResult> Items,
    int Page,
    int PageSize,
    int TotalItems,
    int TotalPages);

public sealed record JobPositionInput(string Name);

public sealed record EndEmploymentRelationshipInput(DateOnly EndedOn);

public sealed record EndEmployeeAssignmentInput(DateOnly EndedOn);

public sealed record TransitionEmployeeAssignmentInput(
    Guid BranchId,
    Guid JobPositionId,
    DateOnly EffectiveOn);

public sealed record JobPositionResult(
    Guid Id,
    string Name,
    string Status);

public static class EmployeeErrorCodes
{
    public const string NotFound = "employee.not_found";
    public const string DuplicateDocument = "employee.duplicate_document";
    public const string InvalidData = "employee.invalid_data";
    public const string InvalidBranch = "employee.invalid_branch";
    public const string InvalidJobPosition = "employee.invalid_job_position";
    public const string DuplicateAssignment = "employee.duplicate_assignment";
    public const string DuplicateAccount = "employee.account_already_exists";
    public const string DuplicateEmail = "account.email_already_exists";
    public const string JobPositionDuplicateName = "job_position.duplicate_name";
    public const string JobPositionNotFound = "job_position.not_found";
    public const string JobPositionInvalidStatus = "job_position.invalid_status_transition";
    public const string RelationshipNotFound = "employment_relationship.not_found";
    public const string RelationshipInvalidState = "employment_relationship.invalid_state";
    public const string AssignmentNotFound = "employee_assignment.not_found";
    public const string AssignmentInvalidState = "employee_assignment.invalid_state";
    public const string InvalidDate = "employment.invalid_date";
    public const string Forbidden = "employee.forbidden";
}

public enum EmployeeErrorKind
{
    Validation,
    NotFound,
    Conflict,
    Forbidden
}

public sealed class EmployeeException(
    string code,
    string message,
    EmployeeErrorKind kind = EmployeeErrorKind.Validation) : Exception(message)
{
    public string Code { get; } = code;
    public EmployeeErrorKind Kind { get; } = kind;
}
