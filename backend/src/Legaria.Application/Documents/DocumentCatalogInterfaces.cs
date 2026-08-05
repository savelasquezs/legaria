using Legaria.Application.Authentication;
using Legaria.Domain.Documents;

namespace Legaria.Application.Documents;

public sealed record DocumentCategoryQueryItem(DocumentCategory Category, int DocumentTypeCount);
public sealed record DocumentTypeQueryItem(DocumentType DocumentType, DocumentCategory Category);

public interface IDocumentCatalogRepository
{
    Task<IReadOnlyCollection<DocumentCategoryQueryItem>> ListCategoriesAsync(Guid organizationId, DocumentScope? scope, DocumentCatalogStatus? status, string? search, CancellationToken cancellationToken);
    Task<DocumentCategory?> FindCategoryAsync(Guid organizationId, Guid id, CancellationToken cancellationToken);
    Task<bool> CategoryNameExistsAsync(Guid organizationId, DocumentScope scope, string normalizedName, Guid? excludingId, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<DocumentTypeQueryItem>> ListTypesAsync(Guid organizationId, Guid? categoryId, DocumentScope? scope, DocumentCatalogStatus? status, string? search, CancellationToken cancellationToken);
    Task<DocumentTypeQueryItem?> FindTypeAsync(Guid organizationId, Guid id, CancellationToken cancellationToken);
    Task<bool> TypeNameExistsAsync(Guid organizationId, Guid categoryId, string normalizedName, Guid? excludingId, CancellationToken cancellationToken);
    void AddCategory(DocumentCategory category);
    void AddType(DocumentType documentType);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public interface IDocumentCatalogService
{
    Task<IReadOnlyCollection<DocumentCategoryResult>> ListCategoriesAsync(string? scope, string? status, string? search, CurrentAccount actor, CancellationToken cancellationToken);
    Task<DocumentCategoryResult> CreateCategoryAsync(DocumentCategoryInput input, CurrentAccount actor, CancellationToken cancellationToken);
    Task<DocumentCategoryResult> UpdateCategoryAsync(Guid id, UpdateDocumentCategoryInput input, CurrentAccount actor, CancellationToken cancellationToken);
    Task<DocumentCategoryResult> DeactivateCategoryAsync(Guid id, CurrentAccount actor, CancellationToken cancellationToken);
    Task<DocumentCategoryResult> ReactivateCategoryAsync(Guid id, CurrentAccount actor, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<DocumentTypeResult>> ListTypesAsync(Guid? categoryId, string? scope, string? status, string? search, CurrentAccount actor, CancellationToken cancellationToken);
    Task<DocumentTypeResult> CreateTypeAsync(DocumentTypeInput input, CurrentAccount actor, CancellationToken cancellationToken);
    Task<DocumentTypeResult> UpdateTypeAsync(Guid id, DocumentTypeInput input, CurrentAccount actor, CancellationToken cancellationToken);
    Task<DocumentTypeResult> DeactivateTypeAsync(Guid id, CurrentAccount actor, CancellationToken cancellationToken);
    Task<DocumentTypeResult> ReactivateTypeAsync(Guid id, CurrentAccount actor, CancellationToken cancellationToken);
}
