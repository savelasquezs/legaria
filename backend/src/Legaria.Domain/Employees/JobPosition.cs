namespace Legaria.Domain.Employees;

public enum JobPositionStatus
{
    Active,
    Inactive
}

public sealed class JobPosition
{
    private JobPosition()
    {
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string NormalizedName { get; private set; } = string.Empty;
    public JobPositionStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public static JobPosition Create(
        Guid organizationId,
        string name,
        string normalizedName,
        DateTimeOffset now) =>
        new()
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            Name = name,
            NormalizedName = normalizedName,
            Status = JobPositionStatus.Active,
            CreatedAt = now,
            UpdatedAt = now
        };

    public bool Rename(string name, string normalizedName, DateTimeOffset now)
    {
        if (Name == name && NormalizedName == normalizedName)
        {
            return false;
        }

        Name = name;
        NormalizedName = normalizedName;
        UpdatedAt = now;
        return true;
    }

    public bool Deactivate(DateTimeOffset now)
    {
        if (Status == JobPositionStatus.Inactive)
        {
            return false;
        }

        Status = JobPositionStatus.Inactive;
        UpdatedAt = now;
        return true;
    }

    public bool Reactivate(DateTimeOffset now)
    {
        if (Status == JobPositionStatus.Active)
        {
            return false;
        }

        Status = JobPositionStatus.Active;
        UpdatedAt = now;
        return true;
    }
}
