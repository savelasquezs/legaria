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
}
