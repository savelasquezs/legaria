using System.ComponentModel.DataAnnotations;
using Legaria.API.Security;
using Legaria.Application.Authentication;
using Legaria.Application.Documents;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Legaria.API.Controllers;

[ApiController]
[Route("api/tenant/document-categories")]
[Authorize(Policy = AuthorizationPolicies.TenantAdministrator)]
public sealed class DocumentCategoriesController(
    IDocumentCatalogService service,
    ICurrentUser currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<DocumentCategoryResult>>> List(
        [FromQuery] string? scope = null,
        [FromQuery] string? status = null,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default) =>
        Ok(await service.ListCategoriesAsync(scope, status, search, currentUser.ToCurrentAccount(), cancellationToken));

    [HttpPost]
    public async Task<ActionResult<DocumentCategoryResult>> Create(DocumentCategoryInputModel input, CancellationToken cancellationToken)
    {
        var result = await service.CreateCategoryAsync(input.ToRequest(), currentUser.ToCurrentAccount(), cancellationToken);
        return Created(string.Empty, result);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<DocumentCategoryResult>> Update(Guid id, UpdateDocumentCategoryInputModel input, CancellationToken cancellationToken) =>
        Ok(await service.UpdateCategoryAsync(id, input.ToRequest(), currentUser.ToCurrentAccount(), cancellationToken));

    [HttpPost("{id:guid}/deactivate")]
    public async Task<ActionResult<DocumentCategoryResult>> Deactivate(Guid id, CancellationToken cancellationToken) =>
        Ok(await service.DeactivateCategoryAsync(id, currentUser.ToCurrentAccount(), cancellationToken));

    [HttpPost("{id:guid}/reactivate")]
    public async Task<ActionResult<DocumentCategoryResult>> Reactivate(Guid id, CancellationToken cancellationToken) =>
        Ok(await service.ReactivateCategoryAsync(id, currentUser.ToCurrentAccount(), cancellationToken));
}

[ApiController]
[Route("api/tenant/document-types")]
[Authorize(Policy = AuthorizationPolicies.TenantAdministrator)]
public sealed class DocumentTypesController(
    IDocumentCatalogService service,
    ICurrentUser currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<DocumentTypeResult>>> List(
        [FromQuery] Guid? categoryId = null,
        [FromQuery] string? scope = null,
        [FromQuery] string? status = null,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default) =>
        Ok(await service.ListTypesAsync(categoryId, scope, status, search, currentUser.ToCurrentAccount(), cancellationToken));

    [HttpPost]
    public async Task<ActionResult<DocumentTypeResult>> Create(DocumentTypeInputModel input, CancellationToken cancellationToken)
    {
        var result = await service.CreateTypeAsync(input.ToRequest(), currentUser.ToCurrentAccount(), cancellationToken);
        return Created(string.Empty, result);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<DocumentTypeResult>> Update(Guid id, DocumentTypeInputModel input, CancellationToken cancellationToken) =>
        Ok(await service.UpdateTypeAsync(id, input.ToRequest(), currentUser.ToCurrentAccount(), cancellationToken));

    [HttpPost("{id:guid}/deactivate")]
    public async Task<ActionResult<DocumentTypeResult>> Deactivate(Guid id, CancellationToken cancellationToken) =>
        Ok(await service.DeactivateTypeAsync(id, currentUser.ToCurrentAccount(), cancellationToken));

    [HttpPost("{id:guid}/reactivate")]
    public async Task<ActionResult<DocumentTypeResult>> Reactivate(Guid id, CancellationToken cancellationToken) =>
        Ok(await service.ReactivateTypeAsync(id, currentUser.ToCurrentAccount(), cancellationToken));
}

public sealed record DocumentCategoryInputModel(
    [Required, MaxLength(150)] string Name,
    [MaxLength(1000)] string? Description,
    [Required] string Scope)
{
    public DocumentCategoryInput ToRequest() => new(Name, Description, Scope);
}

public sealed record UpdateDocumentCategoryInputModel(
    [Required, MaxLength(150)] string Name,
    [MaxLength(1000)] string? Description)
{
    public UpdateDocumentCategoryInput ToRequest() => new(Name, Description);
}

public sealed record DocumentTypeInputModel(
    [Required] Guid CategoryId,
    [Required, MaxLength(150)] string Name,
    [MaxLength(1000)] string? Description,
    bool IsRequiredByDefault,
    [Required] string IssueDateMode,
    [Required] string ExpirationDateMode,
    bool AllowsMultipleActiveVersions,
    bool AllowsMultipleEvidenceItems,
    [Required, MinLength(1)] IReadOnlyCollection<string> AllowedEvidenceKinds)
{
    public DocumentTypeInput ToRequest() => new(
        CategoryId,
        Name,
        Description,
        IsRequiredByDefault,
        IssueDateMode,
        ExpirationDateMode,
        AllowsMultipleActiveVersions,
        AllowsMultipleEvidenceItems,
        AllowedEvidenceKinds);
}
