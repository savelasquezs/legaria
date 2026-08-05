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
    Task<Employee?> FindAsync(Guid organizationId, Guid employeeId, CancellationToken cancellationToken);
    Task<bool> DocumentExistsAsync(Guid organizationId, string documentType, string documentNumber, CancellationToken cancellationToken);
    Task<EmploymentRelationship?> FindActiveRelationshipAsync(Guid organizationId, Guid employeeId, CancellationToken cancellationToken);
    Task<bool> ActiveAssignmentExistsAsync(Guid organizationId, Guid relationshipId, Guid branchId, CancellationToken cancellationToken);
    Task<bool> ActivePrimaryAssignmentExistsAsync(Guid organizationId, Guid relationshipId, CancellationToken cancellationToken);
    Task<JobPosition?> FindActiveJobPositionAsync(Guid organizationId, Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<JobPosition>> ListActiveJobPositionsAsync(Guid organizationId, CancellationToken cancellationToken);
    Task<bool> JobPositionNameExistsAsync(Guid organizationId, string normalizedName, CancellationToken cancellationToken);
    Task<UserAccount?> FindLinkedAccountAsync(Guid organizationId, Guid employeeId, CancellationToken cancellationToken);
    void AddEmployee(Employee employee);
    void AddRelationship(EmploymentRelationship relationship);
    void AddAssignment(EmployeeAssignment assignment);
    void AddJobPosition(JobPosition position);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public interface IEmployeeService
{
    Task<EmployeePage> ListAsync(int page, int pageSize, string? search, Guid? branchId, Guid? excludeBranchId, CurrentAccount actor, CancellationToken cancellationToken);
    Task<EmployeeResult> GetAsync(Guid id, CurrentAccount actor, CancellationToken cancellationToken);
    Task<EmployeeResult> CreateAsync(Guid branchId, CreateEmployeeInput input, CurrentAccount actor, ClientContext client, CancellationToken cancellationToken);
    Task<EmployeeResult> AssignAsync(Guid branchId, Guid employeeId, AssignEmployeeInput input, CurrentAccount actor, ClientContext client, CancellationToken cancellationToken);
    Task<EmployeeResult> GrantAdministrativeAccessAsync(Guid employeeId, AdministrativeAccessInput input, CurrentAccount actor, ClientContext client, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<JobPositionResult>> ListJobPositionsAsync(CurrentAccount actor, CancellationToken cancellationToken);
    Task<JobPositionResult> CreateJobPositionAsync(JobPositionInput input, CurrentAccount actor, CancellationToken cancellationToken);
}
