using Legaria.Application.Authentication;
using Legaria.Application.Branches;
using Legaria.Application.Employees;
using Legaria.Domain.Authentication;
using Legaria.Domain.Documents;

namespace Legaria.Application.Documents;

public sealed class EmployeeDocumentService(
    IEmployeeDocumentRepository repository,
    IEmployeeRepository employeeRepository,
    IBranchRepository branchRepository,
    IEmployeeDocumentStorage storage,
    IClock clock) : IEmployeeDocumentService
{
    public async Task<EmployeeDocumentSummaryResult> GetSummaryAsync(Guid employeeId, CurrentAccount actor, CancellationToken cancellationToken)
    {
        var (organizationId, visibleBranches) = await AuthorizeAsync(employeeId, actor, cancellationToken);
        return await BuildSummaryAsync(organizationId, employeeId, visibleBranches, cancellationToken);
    }

    public async Task<EmployeeDocumentSummaryResult> UploadAsync(Guid employeeId, UploadEmployeeDocumentInput input, CurrentAccount actor, CancellationToken cancellationToken)
    {
        var (organizationId, visibleBranches) = await AuthorizeAsync(employeeId, actor, cancellationToken);
        var requiredTypes = await repository.ListRequiredTypesAsync(organizationId, employeeId, visibleBranches, cancellationToken);
        var required = requiredTypes.SingleOrDefault(item => item.DocumentType.Id == input.DocumentTypeId)
            ?? throw Invalid("El tipo de documento no es obligatorio para el trabajador o no está disponible.");
        ValidateDates(required.DocumentType, input.IssuedOn, input.ExpiresOn);
        var evidenceCount = input.Files.Count + input.Links.Count;
        if (evidenceCount == 0 || (!required.DocumentType.AllowsMultipleEvidenceItems && evidenceCount > 1))
            throw Invalid("La cantidad de evidencias no es válida para este tipo de documento.");

        var validatedFiles = new List<(EmployeeDocumentFileInput File, string Kind, string Extension, string ContentType)>();
        foreach (var file in input.Files)
        {
            var validated = await ValidateFileAsync(file, cancellationToken);
            if (!required.DocumentType.AllowedEvidenceKinds.Contains(validated.Kind))
                throw Invalid("El formato del archivo no está permitido para este tipo de documento.");
            validatedFiles.Add((file, validated.Kind, validated.Extension, validated.ContentType));
        }

        var links = input.Links.Select(ValidateLink).ToArray();
        if (links.Length > 0 && !required.DocumentType.AllowedEvidenceKinds.Contains(DocumentEvidenceKinds.Link))
            throw Invalid("Los enlaces no están permitidos para este tipo de documento.");

        var uploaded = new List<string>();
        try
        {
            var now = clock.UtcNow;
            var document = EmployeeDocument.Create(organizationId, employeeId, input.DocumentTypeId,
                input.IssuedOn, input.ExpiresOn, actor.UserId, now);
            if (!required.DocumentType.AllowsMultipleActiveVersions)
            {
                foreach (var current in await repository.ListCurrentVersionsAsync(organizationId, employeeId, input.DocumentTypeId, cancellationToken))
                    current.Replace(now);
            }

            var evidences = new List<EmployeeDocumentEvidence>();
            foreach (var file in validatedFiles)
            {
                var objectName = await storage.UploadAsync(file.File.Content, file.Extension, file.ContentType, cancellationToken);
                uploaded.Add(objectName);
                evidences.Add(EmployeeDocumentEvidence.CreateFile(organizationId, document.Id, file.Kind, objectName,
                    CleanFileName(file.File.FileName), file.ContentType, file.File.Length, now));
            }
            evidences.AddRange(links.Select(link => EmployeeDocumentEvidence.CreateLink(organizationId, document.Id, link, now)));
            repository.AddDocument(document);
            repository.AddEvidences(evidences);
            await repository.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            foreach (var objectName in uploaded)
            {
                try { await storage.DeleteAsync(objectName, CancellationToken.None); }
                catch (Exception cleanupException) { System.Diagnostics.Trace.TraceWarning($"No fue posible limpiar una evidencia fallida: {cleanupException.GetType().Name}"); }
            }
            throw;
        }

        return await BuildSummaryAsync(organizationId, employeeId, visibleBranches, cancellationToken);
    }

    public async Task<EmployeeDocumentDownload> DownloadAsync(Guid employeeId, Guid evidenceId, CurrentAccount actor, CancellationToken cancellationToken)
    {
        var (organizationId, _) = await AuthorizeAsync(employeeId, actor, cancellationToken);
        var item = await repository.FindEvidenceAsync(organizationId, employeeId, evidenceId, cancellationToken)
            ?? throw NotFound();
        if (string.IsNullOrWhiteSpace(item.Evidence.StorageKey)) throw NotFound();
        return new EmployeeDocumentDownload(
            await storage.DownloadAsync(item.Evidence.StorageKey, cancellationToken),
            item.Evidence.ContentType ?? "application/octet-stream",
            item.Evidence.OriginalFileName ?? "documento");
    }

    private async Task<EmployeeDocumentSummaryResult> BuildSummaryAsync(Guid organizationId, Guid employeeId, IReadOnlyCollection<Guid>? visibleBranches, CancellationToken cancellationToken)
    {
        var required = await repository.ListRequiredTypesAsync(organizationId, employeeId, visibleBranches, cancellationToken);
        var documents = await repository.ListCurrentDocumentsAsync(organizationId, employeeId, cancellationToken);
        var today = DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);
        var validTypeIds = documents.Where(item => item.Document.ExpiresOn is null || item.Document.ExpiresOn >= today)
            .Select(item => item.Document.DocumentTypeId).ToHashSet();
        var missing = required.Where(item => !validTypeIds.Contains(item.DocumentType.Id)).ToArray();
        var upcoming = documents.Where(item => item.Document.ExpiresOn is { } expires && expires >= today && expires <= today.AddMonths(2))
            .OrderBy(item => item.Document.ExpiresOn)
            .Select(item => new UpcomingEmployeeDocumentResult(item.Document.Id, item.DocumentType.Id, item.DocumentType.Name, item.Category.Name, item.Document.ExpiresOn!.Value))
            .ToArray();
        var missingIds = missing.Select(item => item.DocumentType.Id).ToHashSet();
        var categories = required.GroupBy(item => new { item.Category.Id, item.Category.Name })
            .Select(group => new EmployeeDocumentCategoryResult(group.Key.Id, group.Key.Name,
                group.Count(item => missingIds.Contains(item.DocumentType.Id)),
                group.Select(item => new EmployeeDocumentTypeOption(item.DocumentType.Id, item.DocumentType.Name,
                    missingIds.Contains(item.DocumentType.Id), Mode(item.DocumentType.IssueDateMode), Mode(item.DocumentType.ExpirationDateMode),
                    item.DocumentType.AllowsMultipleEvidenceItems, item.DocumentType.AllowedEvidenceKinds)).ToArray())).ToArray();
        return new EmployeeDocumentSummaryResult(required.Count, missing.Length,
            missing.Select(item => new RequiredEmployeeDocumentResult(item.DocumentType.Id, item.DocumentType.Name, item.Category.Id, item.Category.Name)).ToArray(),
            upcoming, categories);
    }

    private async Task<(Guid OrganizationId, IReadOnlyCollection<Guid>? VisibleBranches)> AuthorizeAsync(Guid employeeId, CurrentAccount actor, CancellationToken cancellationToken)
    {
        if (actor.AccountType != AccountType.Tenant || actor.OrganizationId is not { } organizationId ||
            !actor.Roles.Any(role => role is SystemRoleCodes.SuperAdmin or SystemRoleCodes.BranchAdmin)) throw Forbidden();
        if (actor.Roles.Contains(SystemRoleCodes.SuperAdmin))
        {
            _ = await employeeRepository.FindAsync(organizationId, employeeId, cancellationToken) ?? throw NotFound();
            return (organizationId, null);
        }
        if (actor.EmployeeId == employeeId) throw Forbidden();
        var detail = await employeeRepository.FindEmploymentDetailsAsync(organizationId, employeeId, cancellationToken) ?? throw NotFound();
        var branches = (await branchRepository.FindActiveAccessesAsync(organizationId, actor.UserId, cancellationToken)).Select(item => item.BranchId).ToArray();
        if (!detail.Relationships.SelectMany(item => item.Assignments).Any(item => item.Assignment.EndedOn is null && branches.Contains(item.Assignment.BranchId))) throw NotFound();
        return (organizationId, branches);
    }

    private void ValidateDates(DocumentType type, DateOnly? issuedOn, DateOnly? expiresOn)
    {
        var today = DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);
        if ((type.IssueDateMode == DocumentDateMode.Required && issuedOn is null) || (type.IssueDateMode == DocumentDateMode.Never && issuedOn is not null) || issuedOn > today)
            throw Invalid("La fecha de expedición no es válida.");
        if ((type.ExpirationDateMode == DocumentDateMode.Required && expiresOn is null) || (type.ExpirationDateMode == DocumentDateMode.Never && expiresOn is not null) || expiresOn < today || (issuedOn is not null && expiresOn < issuedOn))
            throw Invalid("La fecha de vencimiento no es válida.");
    }

    private static async Task<(string Kind, string Extension, string ContentType)> ValidateFileAsync(EmployeeDocumentFileInput file, CancellationToken cancellationToken)
    {
        if (file.Length <= 0 || file.Length > 100 * 1024 * 1024 || !file.Content.CanSeek) throw Invalid("El archivo está vacío, excede 100 MB o no puede validarse.");
        var header = new byte[12];
        var read = await file.Content.ReadAsync(header.AsMemory(), cancellationToken);
        file.Content.Position = 0;
        if (read >= 5 && header.AsSpan(0, 5).SequenceEqual("%PDF-"u8)) return (DocumentEvidenceKinds.Pdf, ".pdf", "application/pdf");
        if (read >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF) return (DocumentEvidenceKinds.Image, ".jpg", "image/jpeg");
        if (read >= 8 && header.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })) return (DocumentEvidenceKinds.Image, ".png", "image/png");
        if (read >= 12 && header.AsSpan(0, 4).SequenceEqual("RIFF"u8) && header.AsSpan(8, 4).SequenceEqual("WEBP"u8)) return (DocumentEvidenceKinds.Image, ".webp", "image/webp");
        if (read >= 8 && header.AsSpan(4, 4).SequenceEqual("ftyp"u8)) return (DocumentEvidenceKinds.Video, ".mp4", "video/mp4");
        if (read >= 4 && header.AsSpan(0, 4).SequenceEqual(new byte[] { 0x1A, 0x45, 0xDF, 0xA3 })) return (DocumentEvidenceKinds.Video, ".webm", "video/webm");
        throw Invalid("El contenido del archivo no corresponde a PDF, imagen o video admitido.");
    }

    private static string ValidateLink(string value) => Uri.TryCreate(value?.Trim(), UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps && value.Length <= 2048 ? uri.AbsoluteUri : throw Invalid("Los enlaces deben usar HTTPS y tener máximo 2048 caracteres.");
    private static string CleanFileName(string value) { var name = Path.GetFileName(value.Trim()); return string.IsNullOrWhiteSpace(name) ? "documento" : name[..Math.Min(name.Length, 255)]; }
    private static string Mode(DocumentDateMode value) => value == DocumentDateMode.Never ? "NEVER" : value == DocumentDateMode.Optional ? "OPTIONAL" : "REQUIRED";
    private static EmployeeDocumentException Invalid(string message) => new(EmployeeDocumentErrorCodes.InvalidData, message);
    private static EmployeeDocumentException NotFound() => new(EmployeeDocumentErrorCodes.NotFound, "El documento o trabajador no existe.", EmployeeDocumentErrorKind.NotFound);
    private static EmployeeDocumentException Forbidden() => new(EmployeeDocumentErrorCodes.Forbidden, "No tienes permiso para gestionar estos documentos.", EmployeeDocumentErrorKind.Forbidden);
}
