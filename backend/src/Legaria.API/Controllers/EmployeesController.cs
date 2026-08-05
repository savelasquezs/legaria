using System.ComponentModel.DataAnnotations;
using Legaria.API.Security;
using Legaria.Application.Authentication;
using Legaria.Application.Employees;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Legaria.API.Controllers;

[ApiController]
[Route("api/tenant/employees")]
[Authorize(Policy = AuthorizationPolicies.TenantSuperAdministrator)]
public sealed class EmployeesController(
    IEmployeeService employeeService,
    ICurrentUser currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<EmployeePage>> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] Guid? branchId = null,
        [FromQuery] Guid? excludeBranchId = null,
        CancellationToken cancellationToken = default) =>
        Ok(await employeeService.ListAsync(
            page,
            pageSize,
            search,
            branchId,
            excludeBranchId,
            currentUser.ToCurrentAccount(),
            cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<EmployeeDetailResult>> GetById(Guid id, CancellationToken cancellationToken) =>
        Ok(await employeeService.GetAsync(id, currentUser.ToCurrentAccount(), cancellationToken));

    [HttpPost("/api/tenant/branches/{branchId:guid}/employees")]
    public async Task<ActionResult<EmployeeDetailResult>> Create(
        Guid branchId,
        CreateEmployeeInputModel input,
        CancellationToken cancellationToken)
    {
        var result = await employeeService.CreateAsync(
            branchId,
            input.ToRequest(),
            currentUser.ToCurrentAccount(),
            Client(),
            cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPost("/api/tenant/branches/{branchId:guid}/employees/{employeeId:guid}/assignments")]
    public async Task<ActionResult<EmployeeDetailResult>> Assign(
        Guid branchId,
        Guid employeeId,
        AssignEmployeeInputModel input,
        CancellationToken cancellationToken) =>
        Ok(await employeeService.AssignAsync(
            branchId,
            employeeId,
            input.ToRequest(),
            currentUser.ToCurrentAccount(),
            Client(),
            cancellationToken));

    [HttpPost("{id:guid}/administrative-access")]
    public async Task<ActionResult<EmployeeDetailResult>> GrantAdministrativeAccess(
        Guid id,
        AdministrativeAccessInputModel input,
        CancellationToken cancellationToken) =>
        Ok(await employeeService.GrantAdministrativeAccessAsync(
            id,
            input.ToRequest(),
            currentUser.ToCurrentAccount(),
            Client(),
            cancellationToken));

    [HttpPost("{employeeId:guid}/employment-relationships/{relationshipId:guid}/end")]
    public async Task<ActionResult<EmployeeDetailResult>> EndRelationship(
        Guid employeeId,
        Guid relationshipId,
        EndEmploymentRelationshipInputModel input,
        CancellationToken cancellationToken) =>
        Ok(await employeeService.EndRelationshipAsync(
            employeeId,
            relationshipId,
            new EndEmploymentRelationshipInput(input.EndedOn),
            currentUser.ToCurrentAccount(),
            Client(),
            cancellationToken));

    [HttpPost("{employeeId:guid}/assignments/{assignmentId:guid}/end")]
    public async Task<ActionResult<EmployeeDetailResult>> EndAssignment(
        Guid employeeId,
        Guid assignmentId,
        EndEmployeeAssignmentInputModel input,
        CancellationToken cancellationToken) =>
        Ok(await employeeService.EndAssignmentAsync(
            employeeId,
            assignmentId,
            new EndEmployeeAssignmentInput(input.EndedOn),
            currentUser.ToCurrentAccount(),
            Client(),
            cancellationToken));

    [HttpPost("{employeeId:guid}/assignments/{assignmentId:guid}/transition")]
    public async Task<ActionResult<EmployeeDetailResult>> TransitionAssignment(
        Guid employeeId,
        Guid assignmentId,
        TransitionEmployeeAssignmentInputModel input,
        CancellationToken cancellationToken) =>
        Ok(await employeeService.TransitionAssignmentAsync(
            employeeId,
            assignmentId,
            new TransitionEmployeeAssignmentInput(input.BranchId, input.JobPositionId, input.EffectiveOn),
            currentUser.ToCurrentAccount(),
            Client(),
            cancellationToken));

    [HttpPost("{employeeId:guid}/assignments/{assignmentId:guid}/make-primary")]
    public async Task<ActionResult<EmployeeDetailResult>> MakePrimaryAssignment(
        Guid employeeId,
        Guid assignmentId,
        CancellationToken cancellationToken) =>
        Ok(await employeeService.MakePrimaryAssignmentAsync(
            employeeId,
            assignmentId,
            currentUser.ToCurrentAccount(),
            Client(),
            cancellationToken));

    private ClientContext Client() => new(
        HttpContext.Connection.RemoteIpAddress?.ToString(),
        Request.Headers.UserAgent.ToString() is { Length: > 512 } userAgent
            ? userAgent[..512]
            : Request.Headers.UserAgent.ToString());
}

[ApiController]
[Route("api/tenant/job-positions")]
[Authorize(Policy = AuthorizationPolicies.TenantSuperAdministrator)]
public sealed class JobPositionsController(
    IEmployeeService employeeService,
    ICurrentUser currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<JobPositionResult>>> List(
        [FromQuery] string? status = null,
        CancellationToken cancellationToken = default) =>
        Ok(await employeeService.ListJobPositionsAsync(status, currentUser.ToCurrentAccount(), cancellationToken));

    [HttpPost]
    public async Task<ActionResult<JobPositionResult>> Create(
        JobPositionInputModel input,
        CancellationToken cancellationToken)
    {
        var result = await employeeService.CreateJobPositionAsync(
            new JobPositionInput(input.Name),
            currentUser.ToCurrentAccount(),
            cancellationToken);
        return Created(string.Empty, result);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<JobPositionResult>> Update(
        Guid id,
        JobPositionInputModel input,
        CancellationToken cancellationToken) =>
        Ok(await employeeService.UpdateJobPositionAsync(
            id,
            new JobPositionInput(input.Name),
            currentUser.ToCurrentAccount(),
            cancellationToken));

    [HttpPost("{id:guid}/deactivate")]
    public async Task<ActionResult<JobPositionResult>> Deactivate(Guid id, CancellationToken cancellationToken) =>
        Ok(await employeeService.DeactivateJobPositionAsync(id, currentUser.ToCurrentAccount(), cancellationToken));

    [HttpPost("{id:guid}/reactivate")]
    public async Task<ActionResult<JobPositionResult>> Reactivate(Guid id, CancellationToken cancellationToken) =>
        Ok(await employeeService.ReactivateJobPositionAsync(id, currentUser.ToCurrentAccount(), cancellationToken));
}

public sealed record AdministrativeAccessInputModel(
    [EmailAddress, MaxLength(320)] string? Email,
    [Required, MinLength(1)] IReadOnlyCollection<Guid> BranchIds)
{
    public AdministrativeAccessInput ToRequest() => new(Email, BranchIds);
}

public sealed record CreateEmployeeInputModel(
    [Required, MaxLength(50)] string DocumentType,
    [Required, MaxLength(50)] string DocumentNumber,
    [Required, MaxLength(100)] string FirstName,
    [Required, MaxLength(100)] string LastName,
    [Required] Guid JobPositionId,
    [Required] DateOnly StartedOn,
    bool IsPrimary,
    AdministrativeAccessInputModel? AdministrativeAccess)
{
    public CreateEmployeeInput ToRequest() => new(
        DocumentType,
        DocumentNumber,
        FirstName,
        LastName,
        JobPositionId,
        StartedOn,
        IsPrimary,
        AdministrativeAccess?.ToRequest());
}

public sealed record AssignEmployeeInputModel(
    [Required] Guid JobPositionId,
    [Required] DateOnly StartedOn,
    bool IsPrimary,
    AdministrativeAccessInputModel? AdministrativeAccess)
{
    public AssignEmployeeInput ToRequest() => new(
        JobPositionId,
        StartedOn,
        IsPrimary,
        AdministrativeAccess?.ToRequest());
}

public sealed record JobPositionInputModel([Required, MaxLength(150)] string Name);

public sealed record EndEmploymentRelationshipInputModel([Required] DateOnly EndedOn);

public sealed record EndEmployeeAssignmentInputModel([Required] DateOnly EndedOn);

public sealed record TransitionEmployeeAssignmentInputModel(
    [Required] Guid BranchId,
    [Required] Guid JobPositionId,
    [Required] DateOnly EffectiveOn);
