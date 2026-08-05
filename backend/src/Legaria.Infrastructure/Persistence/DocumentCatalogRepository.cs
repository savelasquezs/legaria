using Legaria.Application.Documents;
using Legaria.Domain.Documents;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Legaria.Infrastructure.Persistence;

public sealed class DocumentCatalogRepository(LegariaDbContext dbContext) : IDocumentCatalogRepository
{
    public async Task<IReadOnlyCollection<DocumentCategoryQueryItem>> ListCategoriesAsync(
        Guid organizationId,
        DocumentScope? scope,
        DocumentCatalogStatus? status,
        string? search,
        CancellationToken cancellationToken)
    {
        var query = dbContext.DocumentCategories.AsNoTracking().Where(item => item.OrganizationId == organizationId);
        if (scope is not null) query = query.Where(item => item.Scope == scope);
        if (status is not null) query = query.Where(item => item.Status == status);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{EscapeLike(search)}%";
            query = query.Where(item => EF.Functions.ILike(item.Name, pattern, "\\") ||
                (item.Description != null && EF.Functions.ILike(item.Description, pattern, "\\")));
        }

        return await query
            .OrderBy(item => item.Name)
            .ThenBy(item => item.Id)
            .Select(item => new DocumentCategoryQueryItem(
                item,
                dbContext.DocumentTypes.Count(type => type.OrganizationId == organizationId && type.CategoryId == item.Id)))
            .ToArrayAsync(cancellationToken);
    }

    public Task<DocumentCategory?> FindCategoryAsync(Guid organizationId, Guid id, CancellationToken cancellationToken) =>
        dbContext.DocumentCategories.SingleOrDefaultAsync(item => item.OrganizationId == organizationId && item.Id == id, cancellationToken);

    public Task<bool> CategoryNameExistsAsync(Guid organizationId, DocumentScope scope, string normalizedName, Guid? excludingId, CancellationToken cancellationToken) =>
        dbContext.DocumentCategories.AnyAsync(item => item.OrganizationId == organizationId && item.Scope == scope && item.NormalizedName == normalizedName && item.Id != excludingId, cancellationToken);

    public async Task<IReadOnlyCollection<DocumentTypeQueryItem>> ListTypesAsync(
        Guid organizationId,
        Guid? categoryId,
        DocumentScope? scope,
        DocumentCatalogStatus? status,
        string? search,
        CancellationToken cancellationToken)
    {
        var query =
            from documentType in dbContext.DocumentTypes.AsNoTracking()
            join category in dbContext.DocumentCategories.AsNoTracking()
                on new { documentType.OrganizationId, Id = documentType.CategoryId }
                equals new { category.OrganizationId, category.Id }
            where documentType.OrganizationId == organizationId
            select new { DocumentType = documentType, Category = category };
        if (categoryId is not null) query = query.Where(item => item.DocumentType.CategoryId == categoryId);
        if (scope is not null) query = query.Where(item => item.Category.Scope == scope);
        if (status is not null) query = query.Where(item => item.DocumentType.Status == status);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{EscapeLike(search)}%";
            query = query.Where(item => EF.Functions.ILike(item.DocumentType.Name, pattern, "\\") ||
                (item.DocumentType.Description != null && EF.Functions.ILike(item.DocumentType.Description, pattern, "\\")));
        }

        return await query
            .OrderBy(item => item.DocumentType.Name)
            .ThenBy(item => item.DocumentType.Id)
            .Select(item => new DocumentTypeQueryItem(item.DocumentType, item.Category))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<DocumentTypeQueryItem?> FindTypeAsync(Guid organizationId, Guid id, CancellationToken cancellationToken)
    {
        var documentType = await dbContext.DocumentTypes.SingleOrDefaultAsync(item => item.OrganizationId == organizationId && item.Id == id, cancellationToken);
        if (documentType is null) return null;
        var category = await dbContext.DocumentCategories.SingleAsync(item => item.OrganizationId == organizationId && item.Id == documentType.CategoryId, cancellationToken);
        return new DocumentTypeQueryItem(documentType, category);
    }

    public Task<bool> TypeNameExistsAsync(Guid organizationId, Guid categoryId, string normalizedName, Guid? excludingId, CancellationToken cancellationToken) =>
        dbContext.DocumentTypes.AnyAsync(item => item.OrganizationId == organizationId && item.CategoryId == categoryId && item.NormalizedName == normalizedName && item.Id != excludingId, cancellationToken);

    public void AddCategory(DocumentCategory category) => dbContext.DocumentCategories.Add(category);
    public void AddType(DocumentType documentType) => dbContext.DocumentTypes.Add(documentType);

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException postgres && postgres.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            if (postgres.ConstraintName == "ix_document_categories_organization_id_scope_normalized_name")
            {
                throw new DocumentCatalogException(DocumentCatalogErrorCodes.CategoryDuplicateName, "Ya existe una categoría con ese nombre y alcance.", DocumentCatalogErrorKind.Conflict);
            }

            if (postgres.ConstraintName == "ix_document_types_organization_id_category_id_normalized_name")
            {
                throw new DocumentCatalogException(DocumentCatalogErrorCodes.TypeDuplicateName, "Ya existe un tipo de documento con ese nombre en la categoría.", DocumentCatalogErrorKind.Conflict);
            }

            throw;
        }
    }

    private static string EscapeLike(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);
}
