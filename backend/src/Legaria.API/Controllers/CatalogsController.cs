using Legaria.API.Security;
using Legaria.Application.Organizations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Legaria.API.Controllers;

[ApiController]
[Route("api/catalogs")]
[Authorize(Policy = AuthorizationPolicies.AuthenticatedAccount)]
public sealed class CatalogsController(IOrganizationService organizationService) : ControllerBase
{
    [HttpGet("departments")]
    public async Task<ActionResult<IReadOnlyCollection<DepartmentResult>>> Departments(
        CancellationToken cancellationToken) =>
        Ok(await organizationService.GetDepartmentsAsync(cancellationToken));

    [HttpGet("departments/{code}/municipalities")]
    public async Task<ActionResult<IReadOnlyCollection<MunicipalityResult>>> Municipalities(
        string code,
        CancellationToken cancellationToken) =>
        Ok(await organizationService.GetMunicipalitiesAsync(code, cancellationToken));
}
