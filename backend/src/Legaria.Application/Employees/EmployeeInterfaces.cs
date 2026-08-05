using Legaria.Application.Authentication;
using Legaria.Domain.Authentication;
using Legaria.Domain.Employees;
using Legaria.Domain.Tenancy;

namespace Legaria.Application.Employees;

public sealed record EmployeeAssignmentQueryItem(
    EmployeeAssignment Assignment,
    Branch Branch,
    JobPosition JobPosition);

public sealed record EmployeeQueryItem(
    Employee Employee,
    IReadOnlyCollection<EmployeeAssignmentQueryItem> Assignments,
    UserAccount? Account,
    AccountToken? Invitation,
    IReadOnlyCollection<Guid> AdministrativeBranchIds);

public sealed record EmploymentRelationshipQueryItem(
    EmploymentRelationship Relationship,
    IReadOnlyCollection<EmployeeAssignmentQueryItem> Assignments);

public sealed record EmployeeDetailQueryItem(
    Employee Employee,
    IReadOnlyCollection<EmploymentRelationshipQueryItem> Relationships,
    UserAccount? Account,
    AccountToken? Invitation,
    IReadOnlyCollection<Guid> AdministrativeBranchIds);

public interface IEmployeeTransaction : IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken);
}

public interface IEmployeeRepository
{
    Task<(IReadOnlyCollection<EmployeeQueryItem> Items, int Total)> ListAsync(
        Guid organizationId,
        Guid? branchId,
        Guid? excludeBranchId,
        int skip,
        int take,
        string? search,
        CancellationToken cancellationToken);
    Task<EmployeeQueryItem?> FindDetailsAsync(Guid organizationId, Guid employeeId, CancellationToken cancellationToken);
    Task<EmployeeDetailQueryItem?> FindEmploymentDetailsAsync(Guid organizationId, Guid employeeId, CancellationToken cancellationToken);
    Task<Employee?> FindAsync(Guid organizationId, Guid employeeId, CancellationToken cancellationToken);
    Task<bool> DocumentExistsAsync(Guid organizationId, string documentType, string documentNumber, CancellationToken cancellationToken);
    Task<EmploymentRelationship?> FindActiveRelationshipAsync(Guid organizationId, Guid employeeId, CancellationToken cancellationToken);
    Task<EmploymentRelationship?> FindLatestRelationshipAsync(Guid organizationId, Guid employeeId, CancellationToken cancellationToken);
    Task<EmploymentRelationship?> FindRelationshipAsync(Guid organizationId, Guid employeeId, Guid relationshipId, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<EmployeeAssignment>> FindActiveAssignmentsAsync(Guid organizationId, Guid relationshipId, CancellationToken cancellationToken);
    Task<EmployeeAssignment?> FindAssignmentAsync(Guid organizationId, Guid employeeId, Guid assignmentId, CancellationToken cancellationToken);
    Task<bool> ActiveAssignmentExistsAsync(Guid organizationId, Guid relationshipId, Guid branchId, CancellationToken cancellationToken);
    Task<bool> ActivePrimaryAssignmentExistsAsync(Guid organizationId, Guid relationshipId, CancellationToken cancellationToken);
    Task<bool> AssignmentPeriodOverlapsAsync(Guid organizationId, Guid relationshipId, Guid branchId, DateOnly startedOn, DateOnly? endedOn, Guid? excludingAssignmentId, CancellationToken cancellationToken);
    Task<JobPosition?> FindActiveJobPositionAsync(Guid organizationId, Guid id, CancellationToken cancellationToken);
    Task<JobPosition?> FindJobPositionAsync(Guid organizationId, Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<JobPosition>> ListJobPositionsAsync(Guid organizationId, JobPositionStatus? status, CancellationToken cancellationToken);
    Task<bool> JobPositionNameExistsAsync(Guid organizationId, string normalizedName, Guid? excludingId, CancellationToken cancellationToken);
    Task<UserAccount?> FindLinkedAccountAsync(Guid organizationId, Guid employeeId, CancellationToken cancellationToken);
    void AddEmployee(Employee employee);
    void AddRelationship(EmploymentRelationship relationship);
    void AddAssignment(EmployeeAssignment assignment);
    void AddJobPosition(JobPosition position);
    Task<IEmployeeTransaction> BeginTransactionAsync(CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public interface IEmployeeService
{
    Task<EmployeePage> ListAsync(int page, int pageSize, string? search, Guid? branchId, Guid? excludeBranchId, CurrentAccount actor, CancellationToken cancellationToken);
    Task<EmployeeDetailResult> GetAsync(Guid id, CurrentAccount actor, CancellationToken cancellationToken);
    Task<EmployeeDetailResult> CreateAsync(Guid branchId, CreateEmployeeInput input, CurrentAccount actor, ClientContext client, CancellationToken cancellationToken);
    Task<EmployeeDetailResult> AssignAsync(Guid branchId, Guid employeeId, AssignEmployeeInput input, CurrentAccount actor, ClientContext client, CancellationToken cancellationToken);
    Task<EmployeeDetailResult> GrantAdministrativeAccessAsync(Guid employeeId, AdministrativeAccessInput input, CurrentAccount actor, ClientContext client, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<JobPositionResult>> ListJobPositionsAsync(string? status, CurrentAccount actor, CancellationToken cancellationToken);
    Task<JobPositionResult> CreateJobPositionAsync(JobPositionInput input, CurrentAccount actor, CancellationToken cancellationToken);
    Task<JobPositionResult> UpdateJobPositionAsync(Guid id, JobPositionInput input, CurrentAccount actor, CancellationToken cancellationToken);
    Task<JobPositionResult> DeactivateJobPositionAsync(Guid id, CurrentAccount actor, CancellationToken cancellationToken);
    Task<JobPositionResult> ReactivateJobPositionAsync(Guid id, CurrentAccount actor, CancellationToken cancellationToken);
    Task<EmployeeDetailResult> EndRelationshipAsync(Guid employeeId, Guid relationshipId, EndEmploymentRelationshipInput input, CurrentAccount actor, ClientContext client, CancellationToken cancellationToken);
    Task<EmployeeDetailResult> EndAssignmentAsync(Guid employeeId, Guid assignmentId, EndEmployeeAssignmentInput input, CurrentAccount actor, ClientContext client, CancellationToken cancellationToken);
    Task<EmployeeDetailResult> TransitionAssignmentAsync(Guid employeeId, Guid assignmentId, TransitionEmployeeAssignmentInput input, CurrentAccount actor, ClientContext client, CancellationToken cancellationToken);
    Task<EmployeeDetailResult> MakePrimaryAssignmentAsync(Guid employeeId, Guid assignmentId, CurrentAccount actor, ClientContext client, CancellationToken cancellationToken);
}
