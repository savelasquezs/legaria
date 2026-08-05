namespace Legaria.Domain.Documents;

public enum DocumentDateMode
{
    Never,
    Optional,
    Required
}

public static class DocumentEvidenceKinds
{
    public const string Pdf = "PDF";
    public const string Image = "IMAGE";
    public const string Video = "VIDEO";
    public const string Link = "LINK";

    public static readonly IReadOnlyCollection<string> All = [Pdf, Image, Video, Link];
}

public sealed class DocumentType
{
    private DocumentType()
    {
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid CategoryId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string NormalizedName { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public DocumentCatalogStatus Status { get; private set; }
    public bool IsRequiredByDefault { get; private set; }
    public DocumentDateMode IssueDateMode { get; private set; }
    public DocumentDateMode ExpirationDateMode { get; private set; }
    public bool AllowsMultipleActiveVersions { get; private set; }
    public bool AllowsMultipleEvidenceItems { get; private set; }
    public string[] AllowedEvidenceKinds { get; private set; } = [];
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public static DocumentType Create(
        Guid organizationId,
        Guid categoryId,
        string name,
        string normalizedName,
        string? description,
        bool isRequiredByDefault,
        DocumentDateMode issueDateMode,
        DocumentDateMode expirationDateMode,
        bool allowsMultipleActiveVersions,
        bool allowsMultipleEvidenceItems,
        string[] allowedEvidenceKinds,
        DateTimeOffset now) =>
        new()
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            CategoryId = categoryId,
            Name = name,
            NormalizedName = normalizedName,
            Description = description,
            Status = DocumentCatalogStatus.Active,
            IsRequiredByDefault = isRequiredByDefault,
            IssueDateMode = issueDateMode,
            ExpirationDateMode = expirationDateMode,
            AllowsMultipleActiveVersions = allowsMultipleActiveVersions,
            AllowsMultipleEvidenceItems = allowsMultipleEvidenceItems,
            AllowedEvidenceKinds = allowedEvidenceKinds,
            CreatedAt = now,
            UpdatedAt = now
        };

    public void Update(
        Guid categoryId,
        string name,
        string normalizedName,
        string? description,
        bool isRequiredByDefault,
        DocumentDateMode issueDateMode,
        DocumentDateMode expirationDateMode,
        bool allowsMultipleActiveVersions,
        bool allowsMultipleEvidenceItems,
        string[] allowedEvidenceKinds,
        DateTimeOffset now)
    {
        CategoryId = categoryId;
        Name = name;
        NormalizedName = normalizedName;
        Description = description;
        IsRequiredByDefault = isRequiredByDefault;
        IssueDateMode = issueDateMode;
        ExpirationDateMode = expirationDateMode;
        AllowsMultipleActiveVersions = allowsMultipleActiveVersions;
        AllowsMultipleEvidenceItems = allowsMultipleEvidenceItems;
        AllowedEvidenceKinds = allowedEvidenceKinds;
        UpdatedAt = now;
    }

    public bool Deactivate(DateTimeOffset now)
    {
        if (Status == DocumentCatalogStatus.Inactive) return false;
        Status = DocumentCatalogStatus.Inactive;
        UpdatedAt = now;
        return true;
    }

    public bool Reactivate(DateTimeOffset now)
    {
        if (Status == DocumentCatalogStatus.Active) return false;
        Status = DocumentCatalogStatus.Active;
        UpdatedAt = now;
        return true;
    }
}
