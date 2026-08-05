using System.ComponentModel.DataAnnotations;
using Legaria.API.Security;
using Legaria.Application.Authentication;
using Legaria.Application.Branches;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Legaria.API.Controllers;

[ApiController]
[Route("api/tenant/branch-administrators")]
[Authorize(Policy = AuthorizationPolicies.TenantSuperAdministrator)]
public sealed class BranchAdministratorsController(
    IBranchService branchService,
    ICurrentUser currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<BranchAdministratorPage>> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] string? status = null,
        CancellationToken cancellationToken = default) =>
        Ok(await branchService.ListAdministratorsAsync(
            page,
            pageSize,
            search,
            status,
            currentUser.ToCurrentAccount(),
            cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<BranchAdministratorResult>> GetById(
        Guid id,
        CancellationToken cancellationToken) =>
        Ok(await branchService.GetAdministratorAsync(id, currentUser.ToCurrentAccount(), cancellationToken));

    [HttpPut("{id:guid}/pending-profile")]
    public async Task<ActionResult<BranchAdministratorResult>> UpdatePending(
        Guid id,
        BranchAdministratorInputModel input,
        CancellationToken cancellationToken) =>
        Ok(await branchService.UpdatePendingAdministratorAsync(
            id,
            input.ToRequest(),
            currentUser.ToCurrentAccount(),
            Client(),
            cancellationToken));

    [HttpPut("{id:guid}/branches")]
    public async Task<ActionResult<BranchAdministratorResult>> UpdateBranches(
        Guid id,
        BranchAssignmentsInput input,
        CancellationToken cancellationToken) =>
        Ok(await branchService.UpdateAssignmentsAsync(
            id,
            new UpdateBranchAssignmentsRequest(input.BranchIds),
            currentUser.ToCurrentAccount(),
            Client(),
            cancellationToken));

    [HttpPost("{id:guid}/invitations")]
    public async Task<ActionResult<BranchAdministratorResult>> Resend(
        Guid id,
        CancellationToken cancellationToken) =>
        Ok(await branchService.ResendInvitationAsync(
            id,
            currentUser.ToCurrentAccount(),
            Client(),
            cancellationToken));

    [HttpPost("{id:guid}/suspend")]
    public async Task<ActionResult<BranchAdministratorResult>> Suspend(
        Guid id,
        CancellationToken cancellationToken) =>
        Ok(await branchService.SuspendAdministratorAsync(
            id,
            currentUser.ToCurrentAccount(),
            Client(),
            cancellationToken));

    [HttpPost("{id:guid}/reactivate")]
    public async Task<ActionResult<BranchAdministratorResult>> Reactivate(
        Guid id,
        CancellationToken cancellationToken) =>
        Ok(await branchService.ReactivateAdministratorAsync(
            id,
            currentUser.ToCurrentAccount(),
            Client(),
            cancellationToken));

    private ClientContext Client() => new(
        HttpContext.Connection.RemoteIpAddress?.ToString(),
        Request.Headers.UserAgent.ToString() is { Length: > 512 } userAgent
            ? userAgent[..512]
            : Request.Headers.UserAgent.ToString());
}

public sealed record BranchAdministratorInputModel(
    [Required, MaxLength(100)] string FirstName,
    [Required, MaxLength(100)] string LastName,
    [Required, EmailAddress, MaxLength(320)] string Email,
    [Required, MinLength(1)] IReadOnlyCollection<Guid> BranchIds)
{
    public BranchAdministratorInput ToRequest() => new(FirstName, LastName, Email, BranchIds);
}

public sealed record BranchAssignmentsInput(
    [Required, MinLength(1)] IReadOnlyCollection<Guid> BranchIds);
