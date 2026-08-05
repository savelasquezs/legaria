namespace Legaria.Application.Documents;

public sealed record DocumentCategoryInput(string Name, string? Description, string Scope);
public sealed record UpdateDocumentCategoryInput(string Name, string? Description);

public sealed record DocumentTypeInput(
    Guid CategoryId,
    string Name,
    string? Description,
    bool IsRequiredByDefault,
    string IssueDateMode,
    string ExpirationDateMode,
    bool AllowsMultipleActiveVersions,
    bool AllowsMultipleEvidenceItems,
    IReadOnlyCollection<string> AllowedEvidenceKinds);

public sealed record DocumentCategoryResult(
    Guid Id,
    string Name,
    string? Description,
    string Scope,
    string Status,
    int DocumentTypeCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record DocumentTypeResult(
    Guid Id,
    Guid CategoryId,
    string CategoryName,
    string Scope,
    string Name,
    string? Description,
    string Status,
    bool IsAvailable,
    bool IsRequiredByDefault,
    string IssueDateMode,
    string ExpirationDateMode,
    bool AllowsMultipleActiveVersions,
    bool AllowsMultipleEvidenceItems,
    IReadOnlyCollection<string> AllowedEvidenceKinds,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public static class DocumentCatalogErrorCodes
{
    public const string InvalidData = "document_catalog.invalid_data";
    public const string CategoryNotFound = "document_category.not_found";
    public const string CategoryDuplicateName = "document_category.duplicate_name";
    public const string TypeNotFound = "document_type.not_found";
    public const string TypeDuplicateName = "document_type.duplicate_name";
    public const string InactiveCategory = "document_type.inactive_category";
    public const string ScopeMismatch = "document_type.scope_mismatch";
    public const string Forbidden = "document_catalog.forbidden";
}

public enum DocumentCatalogErrorKind
{
    Validation,
    NotFound,
    Conflict,
    Forbidden
}

public sealed class DocumentCatalogException(
    string code,
    string message,
    DocumentCatalogErrorKind kind = DocumentCatalogErrorKind.Validation) : Exception(message)
{
    public string Code { get; } = code;
    public DocumentCatalogErrorKind Kind { get; } = kind;
}
