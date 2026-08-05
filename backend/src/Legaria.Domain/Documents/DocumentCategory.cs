namespace Legaria.Domain.Documents;

public enum DocumentScope
{
    Employee,
    Branch
}

public enum DocumentCatalogStatus
{
    Active,
    Inactive
}

public sealed class DocumentCategory
{
    private DocumentCategory()
    {
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string NormalizedName { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public DocumentScope Scope { get; private set; }
    public DocumentCatalogStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public static DocumentCategory Create(
        Guid organizationId,
        string name,
        string normalizedName,
        string? description,
        DocumentScope scope,
        DateTimeOffset now) =>
        new()
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            Name = name,
            NormalizedName = normalizedName,
            Description = description,
            Scope = scope,
            Status = DocumentCatalogStatus.Active,
            CreatedAt = now,
            UpdatedAt = now
        };

    public void Update(string name, string normalizedName, string? description, DateTimeOffset now)
    {
        Name = name;
        NormalizedName = normalizedName;
        Description = description;
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
