using Legaria.Application.Authentication;
using Legaria.Domain.Documents;

namespace Legaria.Application.Documents;

public sealed record RequiredEmployeeDocumentResult(Guid DocumentTypeId, string Name, Guid CategoryId, string CategoryName);
public sealed record UpcomingEmployeeDocumentResult(Guid EmployeeDocumentId, Guid DocumentTypeId, string Name, string CategoryName, DateOnly ExpiresOn);
public sealed record EmployeeDocumentCategoryResult(Guid Id, string Name, int MissingCount, IReadOnlyCollection<EmployeeDocumentTypeOption> DocumentTypes);
public sealed record EmployeeDocumentTypeOption(Guid Id, string Name, bool IsMissing, string IssueDateMode, string ExpirationDateMode, bool AllowsMultipleEvidenceItems, IReadOnlyCollection<string> AllowedEvidenceKinds);
public sealed record EmployeeDocumentSummaryResult(int RequiredCount, int MissingCount, IReadOnlyCollection<RequiredEmployeeDocumentResult> MissingDocuments, IReadOnlyCollection<UpcomingEmployeeDocumentResult> UpcomingExpirations, IReadOnlyCollection<EmployeeDocumentCategoryResult> Categories);
public sealed record EmployeeDocumentFileInput(string FileName, string ContentType, long Length, Stream Content);
public sealed record UploadEmployeeDocumentInput(Guid DocumentTypeId, DateOnly? IssuedOn, DateOnly? ExpiresOn, IReadOnlyCollection<EmployeeDocumentFileInput> Files, IReadOnlyCollection<string> Links);
public sealed record EmployeeDocumentDownload(Stream Content, string ContentType, string FileName);

public static class EmployeeDocumentErrorCodes
{
    public const string InvalidData = "employee_document.invalid_data";
    public const string NotFound = "employee_document.not_found";
    public const string Forbidden = "employee_document.forbidden";
}

public sealed class EmployeeDocumentException(string code, string message, EmployeeDocumentErrorKind kind = EmployeeDocumentErrorKind.Validation) : Exception(message)
{
    public string Code { get; } = code;
    public EmployeeDocumentErrorKind Kind { get; } = kind;
}

public enum EmployeeDocumentErrorKind { Validation, NotFound, Forbidden }

public sealed record RequiredDocumentTypeQueryItem(DocumentType DocumentType, DocumentCategory Category);
public sealed record EmployeeDocumentQueryItem(EmployeeDocument Document, DocumentType DocumentType, DocumentCategory Category);
public sealed record EmployeeDocumentEvidenceQueryItem(EmployeeDocumentEvidence Evidence, EmployeeDocument Document);

public interface IEmployeeDocumentRepository
{
    Task<IReadOnlyCollection<RequiredDocumentTypeQueryItem>> ListRequiredTypesAsync(Guid organizationId, Guid employeeId, IReadOnlyCollection<Guid>? visibleBranchIds, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<EmployeeDocumentQueryItem>> ListCurrentDocumentsAsync(Guid organizationId, Guid employeeId, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<EmployeeDocument>> ListCurrentVersionsAsync(Guid organizationId, Guid employeeId, Guid documentTypeId, CancellationToken cancellationToken);
    Task<EmployeeDocumentEvidenceQueryItem?> FindEvidenceAsync(Guid organizationId, Guid employeeId, Guid evidenceId, CancellationToken cancellationToken);
    void AddDocument(EmployeeDocument document);
    void AddEvidences(IEnumerable<EmployeeDocumentEvidence> evidences);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public interface IEmployeeDocumentStorage
{
    Task<string> UploadAsync(Stream content, string extension, string contentType, CancellationToken cancellationToken);
    Task<Stream> DownloadAsync(string objectName, CancellationToken cancellationToken);
    Task DeleteAsync(string objectName, CancellationToken cancellationToken);
}

public interface IEmployeeDocumentService
{
    Task<EmployeeDocumentSummaryResult> GetSummaryAsync(Guid employeeId, CurrentAccount actor, CancellationToken cancellationToken);
    Task<EmployeeDocumentSummaryResult> UploadAsync(Guid employeeId, UploadEmployeeDocumentInput input, CurrentAccount actor, CancellationToken cancellationToken);
    Task<EmployeeDocumentDownload> DownloadAsync(Guid employeeId, Guid evidenceId, CurrentAccount actor, CancellationToken cancellationToken);
}
