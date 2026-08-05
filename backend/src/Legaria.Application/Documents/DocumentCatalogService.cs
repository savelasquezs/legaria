using Legaria.Application.Authentication;
using Legaria.Domain.Authentication;
using Legaria.Domain.Documents;

namespace Legaria.Application.Documents;

public sealed class DocumentCatalogService(
    IDocumentCatalogRepository repository,
    IClock clock) : IDocumentCatalogService
{
    public async Task<IReadOnlyCollection<DocumentCategoryResult>> ListCategoriesAsync(
        string? scope,
        string? status,
        string? search,
        CurrentAccount actor,
        CancellationToken cancellationToken)
    {
        var organizationId = EnsureTenantAdministrator(actor);
        return (await repository.ListCategoriesAsync(
            organizationId,
            ParseOptionalScope(scope),
            ParseStatus(status),
            CleanSearch(search),
            cancellationToken)).Select(ToCategoryResult).ToArray();
    }

    public async Task<DocumentCategoryResult> CreateCategoryAsync(
        DocumentCategoryInput input,
        CurrentAccount actor,
        CancellationToken cancellationToken)
    {
        var organizationId = EnsureTenantAdministrator(actor);
        var scope = ParseScope(input.Scope);
        EnsureCanManage(actor, scope);
        var (name, normalizedName) = CleanName(input.Name);
        if (await repository.CategoryNameExistsAsync(organizationId, scope, normalizedName, null, cancellationToken))
        {
            throw DuplicateCategory();
        }

        var category = DocumentCategory.Create(
            organizationId,
            name,
            normalizedName,
            CleanDescription(input.Description),
            scope,
            clock.UtcNow);
        repository.AddCategory(category);
        await repository.SaveChangesAsync(cancellationToken);
        return ToCategoryResult(new DocumentCategoryQueryItem(category, 0));
    }

    public async Task<DocumentCategoryResult> UpdateCategoryAsync(
        Guid id,
        UpdateDocumentCategoryInput input,
        CurrentAccount actor,
        CancellationToken cancellationToken)
    {
        var organizationId = EnsureTenantAdministrator(actor);
        var category = await FindCategoryAsync(organizationId, id, cancellationToken);
        EnsureCanManage(actor, category.Scope);
        var (name, normalizedName) = CleanName(input.Name);
        if (await repository.CategoryNameExistsAsync(organizationId, category.Scope, normalizedName, id, cancellationToken))
        {
            throw DuplicateCategory();
        }

        category.Update(name, normalizedName, CleanDescription(input.Description), clock.UtcNow);
        await repository.SaveChangesAsync(cancellationToken);
        return ToCategoryResult(new DocumentCategoryQueryItem(category, await CountTypesAsync(organizationId, id, cancellationToken)));
    }

    public Task<DocumentCategoryResult> DeactivateCategoryAsync(Guid id, CurrentAccount actor, CancellationToken cancellationToken) =>
        ChangeCategoryStatusAsync(id, actor, false, cancellationToken);

    public Task<DocumentCategoryResult> ReactivateCategoryAsync(Guid id, CurrentAccount actor, CancellationToken cancellationToken) =>
        ChangeCategoryStatusAsync(id, actor, true, cancellationToken);

    public async Task<IReadOnlyCollection<DocumentTypeResult>> ListTypesAsync(
        Guid? categoryId,
        string? scope,
        string? status,
        string? search,
        CurrentAccount actor,
        CancellationToken cancellationToken)
    {
        var organizationId = EnsureTenantAdministrator(actor);
        if (categoryId is { } id && await repository.FindCategoryAsync(organizationId, id, cancellationToken) is null)
        {
            throw CategoryNotFound();
        }

        return (await repository.ListTypesAsync(
            organizationId,
            categoryId,
            ParseOptionalScope(scope),
            ParseStatus(status),
            CleanSearch(search),
            cancellationToken)).Select(ToTypeResult).ToArray();
    }

    public async Task<DocumentTypeResult> CreateTypeAsync(
        DocumentTypeInput input,
        CurrentAccount actor,
        CancellationToken cancellationToken)
    {
        var organizationId = EnsureTenantAdministrator(actor);
        var category = await FindCategoryAsync(organizationId, input.CategoryId, cancellationToken);
        EnsureCanManage(actor, category.Scope);
        EnsureCategoryActive(category);
        var validated = ValidateType(input);
        if (await repository.TypeNameExistsAsync(organizationId, category.Id, validated.NormalizedName, null, cancellationToken))
        {
            throw DuplicateType();
        }

        var documentType = DocumentType.Create(
            organizationId,
            category.Id,
            validated.Name,
            validated.NormalizedName,
            validated.Description,
            input.IsRequiredByDefault,
            validated.IssueDateMode,
            validated.ExpirationDateMode,
            input.AllowsMultipleActiveVersions,
            input.AllowsMultipleEvidenceItems,
            validated.EvidenceKinds,
            clock.UtcNow);
        repository.AddType(documentType);
        await repository.SaveChangesAsync(cancellationToken);
        return ToTypeResult(new DocumentTypeQueryItem(documentType, category));
    }

    public async Task<DocumentTypeResult> UpdateTypeAsync(
        Guid id,
        DocumentTypeInput input,
        CurrentAccount actor,
        CancellationToken cancellationToken)
    {
        var organizationId = EnsureTenantAdministrator(actor);
        var current = await FindTypeAsync(organizationId, id, cancellationToken);
        EnsureCanManage(actor, current.Category.Scope);
        var targetCategory = await FindCategoryAsync(organizationId, input.CategoryId, cancellationToken);
        EnsureCanManage(actor, targetCategory.Scope);
        if (current.Category.Scope != targetCategory.Scope)
        {
            throw new DocumentCatalogException(
                DocumentCatalogErrorCodes.ScopeMismatch,
                "El tipo solo puede moverse entre categorías del mismo alcance.",
                DocumentCatalogErrorKind.Conflict);
        }

        if (current.DocumentType.CategoryId != targetCategory.Id) EnsureCategoryActive(targetCategory);
        var validated = ValidateType(input);
        if (await repository.TypeNameExistsAsync(organizationId, targetCategory.Id, validated.NormalizedName, id, cancellationToken))
        {
            throw DuplicateType();
        }

        current.DocumentType.Update(
            targetCategory.Id,
            validated.Name,
            validated.NormalizedName,
            validated.Description,
            input.IsRequiredByDefault,
            validated.IssueDateMode,
            validated.ExpirationDateMode,
            input.AllowsMultipleActiveVersions,
            input.AllowsMultipleEvidenceItems,
            validated.EvidenceKinds,
            clock.UtcNow);
        await repository.SaveChangesAsync(cancellationToken);
        return ToTypeResult(new DocumentTypeQueryItem(current.DocumentType, targetCategory));
    }

    public Task<DocumentTypeResult> DeactivateTypeAsync(Guid id, CurrentAccount actor, CancellationToken cancellationToken) =>
        ChangeTypeStatusAsync(id, actor, false, cancellationToken);

    public Task<DocumentTypeResult> ReactivateTypeAsync(Guid id, CurrentAccount actor, CancellationToken cancellationToken) =>
        ChangeTypeStatusAsync(id, actor, true, cancellationToken);

    private async Task<DocumentCategoryResult> ChangeCategoryStatusAsync(Guid id, CurrentAccount actor, bool active, CancellationToken cancellationToken)
    {
        var organizationId = EnsureTenantAdministrator(actor);
        var category = await FindCategoryAsync(organizationId, id, cancellationToken);
        EnsureCanManage(actor, category.Scope);
        if (active) category.Reactivate(clock.UtcNow); else category.Deactivate(clock.UtcNow);
        await repository.SaveChangesAsync(cancellationToken);
        return ToCategoryResult(new DocumentCategoryQueryItem(category, await CountTypesAsync(organizationId, id, cancellationToken)));
    }

    private async Task<DocumentTypeResult> ChangeTypeStatusAsync(Guid id, CurrentAccount actor, bool active, CancellationToken cancellationToken)
    {
        var organizationId = EnsureTenantAdministrator(actor);
        var item = await FindTypeAsync(organizationId, id, cancellationToken);
        EnsureCanManage(actor, item.Category.Scope);
        if (active) item.DocumentType.Reactivate(clock.UtcNow); else item.DocumentType.Deactivate(clock.UtcNow);
        await repository.SaveChangesAsync(cancellationToken);
        return ToTypeResult(item);
    }

    private async Task<int> CountTypesAsync(Guid organizationId, Guid categoryId, CancellationToken cancellationToken) =>
        (await repository.ListTypesAsync(organizationId, categoryId, null, null, null, cancellationToken)).Count;

    private async Task<DocumentCategory> FindCategoryAsync(Guid organizationId, Guid id, CancellationToken cancellationToken) =>
        await repository.FindCategoryAsync(organizationId, id, cancellationToken) ?? throw CategoryNotFound();

    private async Task<DocumentTypeQueryItem> FindTypeAsync(Guid organizationId, Guid id, CancellationToken cancellationToken) =>
        await repository.FindTypeAsync(organizationId, id, cancellationToken) ?? throw TypeNotFound();

    private static (string Name, string NormalizedName, string? Description, DocumentDateMode IssueDateMode, DocumentDateMode ExpirationDateMode, string[] EvidenceKinds) ValidateType(DocumentTypeInput input)
    {
        var (name, normalizedName) = CleanName(input.Name);
        var evidenceKinds = input.AllowedEvidenceKinds
            .Select(value => value?.Trim().ToUpperInvariant() ?? string.Empty)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (evidenceKinds.Length == 0 || evidenceKinds.Any(value => !DocumentEvidenceKinds.All.Contains(value)))
        {
            throw new DocumentCatalogException(DocumentCatalogErrorCodes.InvalidData, "Selecciona al menos un tipo de evidencia válido.");
        }

        return (
            name,
            normalizedName,
            CleanDescription(input.Description),
            ParseDateMode(input.IssueDateMode),
            ParseDateMode(input.ExpirationDateMode),
            DocumentEvidenceKinds.All.Where(evidenceKinds.Contains).ToArray());
    }

    private static (string Name, string NormalizedName) CleanName(string? value)
    {
        var name = string.Join(' ', (value ?? string.Empty).Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries));
        if (name.Length is 0 or > 150)
        {
            throw new DocumentCatalogException(DocumentCatalogErrorCodes.InvalidData, "El nombre es obligatorio y admite máximo 150 caracteres.");
        }

        return (name, name.ToUpperInvariant());
    }

    private static string? CleanDescription(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var description = value.Trim();
        if (description.Length > 1000)
        {
            throw new DocumentCatalogException(DocumentCatalogErrorCodes.InvalidData, "La descripción admite máximo 1000 caracteres.");
        }

        return description;
    }

    private static string? CleanSearch(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var search = value.Trim();
        if (search.Length > 200)
        {
            throw new DocumentCatalogException(DocumentCatalogErrorCodes.InvalidData, "La búsqueda admite máximo 200 caracteres.");
        }

        return search;
    }

    private static DocumentScope ParseScope(string? value) => value?.Trim().ToUpperInvariant() switch
    {
        "EMPLOYEE" => DocumentScope.Employee,
        "BRANCH" => DocumentScope.Branch,
        _ => throw new DocumentCatalogException(DocumentCatalogErrorCodes.InvalidData, "El alcance debe ser EMPLOYEE o BRANCH.")
    };

    private static DocumentScope? ParseOptionalScope(string? value) => value?.Trim().ToUpperInvariant() switch
    {
        null or "" or "ALL" => null,
        "EMPLOYEE" => DocumentScope.Employee,
        "BRANCH" => DocumentScope.Branch,
        _ => throw new DocumentCatalogException(DocumentCatalogErrorCodes.InvalidData, "El alcance no es válido.")
    };

    private static DocumentCatalogStatus? ParseStatus(string? value) => value?.Trim().ToUpperInvariant() switch
    {
        null or "" or "ALL" => null,
        "ACTIVE" => DocumentCatalogStatus.Active,
        "INACTIVE" => DocumentCatalogStatus.Inactive,
        _ => throw new DocumentCatalogException(DocumentCatalogErrorCodes.InvalidData, "El estado no es válido.")
    };

    private static DocumentDateMode ParseDateMode(string? value) => value?.Trim().ToUpperInvariant() switch
    {
        "NEVER" => DocumentDateMode.Never,
        "OPTIONAL" => DocumentDateMode.Optional,
        "REQUIRED" => DocumentDateMode.Required,
        _ => throw new DocumentCatalogException(DocumentCatalogErrorCodes.InvalidData, "El modo de fecha no es válido.")
    };

    private static Guid EnsureTenantAdministrator(CurrentAccount actor)
    {
        if (actor.AccountType != AccountType.Tenant || actor.OrganizationId is not { } organizationId ||
            !actor.Roles.Any(role => role is SystemRoleCodes.SuperAdmin or SystemRoleCodes.BranchAdmin))
        {
            throw Forbidden();
        }

        return organizationId;
    }

    private static void EnsureCanManage(CurrentAccount actor, DocumentScope scope)
    {
        if (!actor.Roles.Contains(SystemRoleCodes.SuperAdmin) && scope != DocumentScope.Branch)
        {
            throw Forbidden();
        }
    }

    private static void EnsureCategoryActive(DocumentCategory category)
    {
        if (category.Status == DocumentCatalogStatus.Inactive)
        {
            throw new DocumentCatalogException(DocumentCatalogErrorCodes.InactiveCategory, "La categoría está inactiva.", DocumentCatalogErrorKind.Conflict);
        }
    }

    private static DocumentCategoryResult ToCategoryResult(DocumentCategoryQueryItem item) => new(
        item.Category.Id,
        item.Category.Name,
        item.Category.Description,
        item.Category.Scope == DocumentScope.Employee ? "EMPLOYEE" : "BRANCH",
        item.Category.Status == DocumentCatalogStatus.Active ? "ACTIVE" : "INACTIVE",
        item.DocumentTypeCount,
        item.Category.CreatedAt,
        item.Category.UpdatedAt);

    private static DocumentTypeResult ToTypeResult(DocumentTypeQueryItem item) => new(
        item.DocumentType.Id,
        item.Category.Id,
        item.Category.Name,
        item.Category.Scope == DocumentScope.Employee ? "EMPLOYEE" : "BRANCH",
        item.DocumentType.Name,
        item.DocumentType.Description,
        item.DocumentType.Status == DocumentCatalogStatus.Active ? "ACTIVE" : "INACTIVE",
        item.DocumentType.Status == DocumentCatalogStatus.Active && item.Category.Status == DocumentCatalogStatus.Active,
        item.DocumentType.IsRequiredByDefault,
        DateModeCode(item.DocumentType.IssueDateMode),
        DateModeCode(item.DocumentType.ExpirationDateMode),
        item.DocumentType.AllowsMultipleActiveVersions,
        item.DocumentType.AllowsMultipleEvidenceItems,
        item.DocumentType.AllowedEvidenceKinds,
        item.DocumentType.CreatedAt,
        item.DocumentType.UpdatedAt);

    private static string DateModeCode(DocumentDateMode value) => value switch
    {
        DocumentDateMode.Never => "NEVER",
        DocumentDateMode.Optional => "OPTIONAL",
        _ => "REQUIRED"
    };

    private static DocumentCatalogException CategoryNotFound() => new(DocumentCatalogErrorCodes.CategoryNotFound, "La categoría no existe.", DocumentCatalogErrorKind.NotFound);
    private static DocumentCatalogException TypeNotFound() => new(DocumentCatalogErrorCodes.TypeNotFound, "El tipo de documento no existe.", DocumentCatalogErrorKind.NotFound);
    private static DocumentCatalogException DuplicateCategory() => new(DocumentCatalogErrorCodes.CategoryDuplicateName, "Ya existe una categoría con ese nombre y alcance.", DocumentCatalogErrorKind.Conflict);
    private static DocumentCatalogException DuplicateType() => new(DocumentCatalogErrorCodes.TypeDuplicateName, "Ya existe un tipo de documento con ese nombre en la categoría.", DocumentCatalogErrorKind.Conflict);
    private static DocumentCatalogException Forbidden() => new(DocumentCatalogErrorCodes.Forbidden, "No tienes permiso para administrar este alcance documental.", DocumentCatalogErrorKind.Forbidden);
}
