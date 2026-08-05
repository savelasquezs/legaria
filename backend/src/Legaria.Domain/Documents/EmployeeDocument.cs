namespace Legaria.Domain.Documents;

public sealed class EmployeeDocument
{
    private EmployeeDocument() { }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid EmployeeId { get; private set; }
    public Guid DocumentTypeId { get; private set; }
    public DateOnly? IssuedOn { get; private set; }
    public DateOnly? ExpiresOn { get; private set; }
    public DateTimeOffset? ReplacedAt { get; private set; }
    public Guid UploadedByUserId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public static EmployeeDocument Create(Guid organizationId, Guid employeeId, Guid documentTypeId,
        DateOnly? issuedOn, DateOnly? expiresOn, Guid uploadedByUserId, DateTimeOffset now) => new()
    {
        Id = Guid.NewGuid(), OrganizationId = organizationId, EmployeeId = employeeId,
        DocumentTypeId = documentTypeId, IssuedOn = issuedOn, ExpiresOn = expiresOn,
        UploadedByUserId = uploadedByUserId, CreatedAt = now
    };

    public void Replace(DateTimeOffset now) => ReplacedAt ??= now;
}

public sealed class EmployeeDocumentEvidence
{
    private EmployeeDocumentEvidence() { }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid EmployeeDocumentId { get; private set; }
    public string Kind { get; private set; } = string.Empty;
    public string? StorageKey { get; private set; }
    public string? OriginalFileName { get; private set; }
    public string? ContentType { get; private set; }
    public long? SizeBytes { get; private set; }
    public string? Url { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public static EmployeeDocumentEvidence CreateFile(Guid organizationId, Guid documentId, string kind,
        string storageKey, string originalFileName, string contentType, long sizeBytes, DateTimeOffset now) => new()
    {
        Id = Guid.NewGuid(), OrganizationId = organizationId, EmployeeDocumentId = documentId, Kind = kind,
        StorageKey = storageKey, OriginalFileName = originalFileName, ContentType = contentType,
        SizeBytes = sizeBytes, CreatedAt = now
    };

    public static EmployeeDocumentEvidence CreateLink(Guid organizationId, Guid documentId, string url, DateTimeOffset now) => new()
    {
        Id = Guid.NewGuid(), OrganizationId = organizationId, EmployeeDocumentId = documentId,
        Kind = DocumentEvidenceKinds.Link, Url = url, CreatedAt = now
    };
}
