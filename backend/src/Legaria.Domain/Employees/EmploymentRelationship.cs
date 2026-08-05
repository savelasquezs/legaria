namespace Legaria.Domain.Employees;

public sealed class EmploymentRelationship
{
    private EmploymentRelationship()
    {
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid EmployeeId { get; private set; }
    public DateOnly StartedOn { get; private set; }
    public DateOnly? EndedOn { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public static EmploymentRelationship Create(
        Guid organizationId,
        Guid employeeId,
        DateOnly startedOn,
        DateTimeOffset now) =>
        new()
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            EmployeeId = employeeId,
            StartedOn = startedOn,
            CreatedAt = now,
            UpdatedAt = now
        };

    public bool End(DateOnly endedOn, DateTimeOffset now)
    {
        if (EndedOn is not null)
        {
            return false;
        }

        EndedOn = endedOn;
        UpdatedAt = now;
        return true;
    }
}
