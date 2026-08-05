using System.ComponentModel.DataAnnotations;
using Legaria.API.Security;
using Legaria.Application.Authentication;
using Legaria.Application.Branches;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Legaria.API.Controllers;

[ApiController]
[Route("api/tenant/branches")]
[Authorize(Policy = AuthorizationPolicies.TenantAdministrator)]
public sealed class BranchesController(
    IBranchService branchService,
    ICurrentUser currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<BranchPage>> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] string? status = null,
        CancellationToken cancellationToken = default) =>
        Ok(await branchService.ListBranchesAsync(
            page,
            pageSize,
            search,
            status,
            currentUser.ToCurrentAccount(),
            cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<BranchResult>> GetById(Guid id, CancellationToken cancellationToken) =>
        Ok(await branchService.GetBranchAsync(id, currentUser.ToCurrentAccount(), cancellationToken));

    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.TenantSuperAdministrator)]
    public async Task<ActionResult<BranchResult>> Create(
        BranchInputModel input,
        CancellationToken cancellationToken)
    {
        var result = await branchService.CreateBranchAsync(
            input.ToRequest(),
            currentUser.ToCurrentAccount(),
            Client(),
            cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.TenantSuperAdministrator)]
    public async Task<ActionResult<BranchResult>> Update(
        Guid id,
        BranchInputModel input,
        CancellationToken cancellationToken) =>
        Ok(await branchService.UpdateBranchAsync(
            id,
            input.ToRequest(),
            currentUser.ToCurrentAccount(),
            Client(),
            cancellationToken));

    [HttpPost("{id:guid}/deactivate")]
    [Authorize(Policy = AuthorizationPolicies.TenantSuperAdministrator)]
    public async Task<ActionResult<BranchResult>> Deactivate(Guid id, CancellationToken cancellationToken) =>
        Ok(await branchService.DeactivateBranchAsync(
            id,
            currentUser.ToCurrentAccount(),
            Client(),
            cancellationToken));

    [HttpPost("{id:guid}/reactivate")]
    [Authorize(Policy = AuthorizationPolicies.TenantSuperAdministrator)]
    public async Task<ActionResult<BranchResult>> Reactivate(Guid id, CancellationToken cancellationToken) =>
        Ok(await branchService.ReactivateBranchAsync(
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

public sealed record BranchInputModel(
    [Required, MaxLength(150)] string Name,
    [EmailAddress, MaxLength(320)] string? ContactEmail,
    [MaxLength(30)] string? Phone,
    [Required, MaxLength(250)] string Address,
    [Required, MaxLength(5)] string MunicipalityCode)
{
    public BranchInput ToRequest() => new(Name, ContactEmail, Phone, Address, MunicipalityCode);
}
