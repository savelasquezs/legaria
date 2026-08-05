using Legaria.API.Security;
using Legaria.Application.Authentication;
using Legaria.Application.Documents;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Legaria.API.Controllers;

[ApiController]
[Route("api/tenant/employees/{employeeId:guid}/documents")]
[Authorize(Policy = AuthorizationPolicies.TenantAdministrator)]
public sealed class EmployeeDocumentsController(IEmployeeDocumentService service, ICurrentUser currentUser) : ControllerBase
{
    [HttpGet("summary")]
    public async Task<ActionResult<EmployeeDocumentSummaryResult>> Summary(Guid employeeId, CancellationToken cancellationToken) =>
        Ok(await service.GetSummaryAsync(employeeId, currentUser.ToCurrentAccount(), cancellationToken));

    [HttpPost]
    [RequestSizeLimit(105 * 1024 * 1024)]
    public async Task<ActionResult<EmployeeDocumentSummaryResult>> Upload(
        Guid employeeId, [FromForm] UploadEmployeeDocumentModel input, CancellationToken cancellationToken)
    {
        var streams = (input.Files ?? []).Select(file => file.OpenReadStream()).ToArray();
        try
        {
            var files = (input.Files ?? []).Select((file, index) =>
                new EmployeeDocumentFileInput(file.FileName, file.ContentType, file.Length, streams[index])).ToArray();
            return Ok(await service.UploadAsync(employeeId,
                new UploadEmployeeDocumentInput(input.DocumentTypeId, input.IssuedOn, input.ExpiresOn, files, input.Links ?? []),
                currentUser.ToCurrentAccount(), cancellationToken));
        }
        finally
        {
            foreach (var stream in streams) await stream.DisposeAsync();
        }
    }

    [HttpGet("evidence/{evidenceId:guid}")]
    public async Task<IActionResult> Download(Guid employeeId, Guid evidenceId, CancellationToken cancellationToken)
    {
        var file = await service.DownloadAsync(employeeId, evidenceId, currentUser.ToCurrentAccount(), cancellationToken);
        return File(file.Content, file.ContentType, file.FileName);
    }
}

public sealed class UploadEmployeeDocumentModel
{
    public Guid DocumentTypeId { get; init; }
    public DateOnly? IssuedOn { get; init; }
    public DateOnly? ExpiresOn { get; init; }
    public IReadOnlyCollection<IFormFile>? Files { get; init; }
    public IReadOnlyCollection<string>? Links { get; init; }
}
