using System.ComponentModel.DataAnnotations;
using Legaria.API.Security;
using Legaria.Application.Authentication;
using Legaria.Application.Organizations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Legaria.API.Controllers;

[ApiController]
[Route("api/platform/organizations")]
[Authorize(Policy = AuthorizationPolicies.PlatformAdminOrOwner)]
public sealed class OrganizationsController(
    IOrganizationService organizationService,
    ICurrentUser currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<OrganizationPage>> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] string? status = null,
        CancellationToken cancellationToken = default) =>
        Ok(await organizationService.ListAsync(page, pageSize, search, status, cancellationToken));

    [HttpPost]
    public async Task<ActionResult<OrganizationResult>> Create(
        CreateOrganizationInput input,
        CancellationToken cancellationToken)
    {
        var result = await organizationService.CreateAsync(
            input.ToRequest(),
            currentUser.ToCurrentAccount(),
            CreateClientContext(),
            cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<OrganizationResult>> GetById(
        Guid id,
        CancellationToken cancellationToken) =>
        Ok(await organizationService.GetAsync(id, cancellationToken));

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<OrganizationResult>> Update(
        Guid id,
        OrganizationInputModel input,
        CancellationToken cancellationToken) =>
        Ok(await organizationService.UpdateAsync(
            id,
            input.ToRequest(),
            currentUser.ToCurrentAccount(),
            CreateClientContext(),
            cancellationToken));

    [HttpPost("{id:guid}/suspend")]
    [Authorize(Policy = AuthorizationPolicies.PlatformOwnerOnly)]
    public async Task<ActionResult<OrganizationResult>> Suspend(
        Guid id,
        CancellationToken cancellationToken) =>
        Ok(await organizationService.SuspendAsync(
            id,
            currentUser.ToCurrentAccount(),
            CreateClientContext(),
            cancellationToken));

    [HttpPost("{id:guid}/reactivate")]
    [Authorize(Policy = AuthorizationPolicies.PlatformOwnerOnly)]
    public async Task<ActionResult<OrganizationResult>> Reactivate(
        Guid id,
        CancellationToken cancellationToken) =>
        Ok(await organizationService.ReactivateAsync(
            id,
            currentUser.ToCurrentAccount(),
            CreateClientContext(),
            cancellationToken));

    [HttpPut("{id:guid}/initial-admin")]
    public async Task<ActionResult<OrganizationResult>> UpdateInitialAdmin(
        Guid id,
        InitialAdministratorInputModel input,
        CancellationToken cancellationToken) =>
        Ok(await organizationService.UpdateInitialAdminAsync(
            id,
            input.ToRequest(),
            currentUser.ToCurrentAccount(),
            CreateClientContext(),
            cancellationToken));

    [HttpPost("{id:guid}/initial-admin/invitations")]
    public async Task<ActionResult<OrganizationResult>> ResendInvitation(
        Guid id,
        CancellationToken cancellationToken) =>
        Ok(await organizationService.ResendInvitationAsync(
            id,
            currentUser.ToCurrentAccount(),
            CreateClientContext(),
            cancellationToken));

    private ClientContext CreateClientContext() => new(
        HttpContext.Connection.RemoteIpAddress?.ToString(),
        Request.Headers.UserAgent.ToString() is { Length: > 512 } userAgent
            ? userAgent[..512]
            : Request.Headers.UserAgent.ToString());
}

public sealed record CreateOrganizationInput(
    [Required, MaxLength(200)] string TradeName,
    [Required, MaxLength(200)] string LegalName,
    [Required, MaxLength(20)] string Nit,
    int VerificationDigit,
    [Required, EmailAddress, MaxLength(320)] string ContactEmail,
    [Required, MaxLength(30)] string Phone,
    [Required, MaxLength(250)] string Address,
    [Required, MaxLength(10)] string MunicipalityCode,
    [Required] InitialAdministratorInputModel InitialAdmin)
{
    public CreateOrganizationRequest ToRequest() => new(
        TradeName,
        LegalName,
        Nit,
        VerificationDigit,
        ContactEmail,
        Phone,
        Address,
        MunicipalityCode,
        InitialAdmin.ToRequest());
}

public sealed record OrganizationInputModel(
    [Required, MaxLength(200)] string TradeName,
    [Required, MaxLength(200)] string LegalName,
    [Required, MaxLength(20)] string Nit,
    int VerificationDigit,
    [Required, EmailAddress, MaxLength(320)] string ContactEmail,
    [Required, MaxLength(30)] string Phone,
    [Required, MaxLength(250)] string Address,
    [Required, MaxLength(10)] string MunicipalityCode)
{
    public UpdateOrganizationRequest ToRequest() => new(
        TradeName,
        LegalName,
        Nit,
        VerificationDigit,
        ContactEmail,
        Phone,
        Address,
        MunicipalityCode);
}

public sealed record InitialAdministratorInputModel(
    [Required, MaxLength(100)] string FirstName,
    [Required, MaxLength(100)] string LastName,
    [Required, EmailAddress, MaxLength(320)] string Email)
{
    public InitialAdministratorInput ToRequest() => new(FirstName, LastName, Email);
}
