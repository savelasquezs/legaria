namespace Legaria.Domain.Employees;

public sealed class EmployeeAssignment
{
    private EmployeeAssignment()
    {
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid EmploymentRelationshipId { get; private set; }
    public Guid BranchId { get; private set; }
    public Guid JobPositionId { get; private set; }
    public bool IsPrimary { get; private set; }
    public DateOnly StartedOn { get; private set; }
    public DateOnly? EndedOn { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public static EmployeeAssignment Create(
        Guid organizationId,
        Guid employmentRelationshipId,
        Guid branchId,
        Guid jobPositionId,
        bool isPrimary,
        DateOnly startedOn,
        DateTimeOffset now) =>
        new()
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            EmploymentRelationshipId = employmentRelationshipId,
            BranchId = branchId,
            JobPositionId = jobPositionId,
            IsPrimary = isPrimary,
            StartedOn = startedOn,
            CreatedAt = now,
            UpdatedAt = now
        };
}
