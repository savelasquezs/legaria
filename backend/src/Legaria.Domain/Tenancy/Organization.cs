using Legaria.Domain.Authentication;

namespace Legaria.Domain.Tenancy;

public sealed class Organization
{
    private Organization()
    {
    }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public OrganizationStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public static Organization Create(string name, DateTimeOffset now)
    {
        return new Organization
        {
            Id = Guid.NewGuid(),
            Name = name,
            Status = OrganizationStatus.Active,
            CreatedAt = now,
            UpdatedAt = now
        };
    }
}
