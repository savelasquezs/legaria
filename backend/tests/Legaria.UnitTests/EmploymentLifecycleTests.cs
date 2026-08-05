using Legaria.Domain.Employees;

namespace Legaria.UnitTests;

public sealed class EmploymentLifecycleTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 5, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void JobPositionSupportsRenameAndStatusChanges()
    {
        var position = JobPosition.Create(Guid.NewGuid(), "Auxiliar", "AUXILIAR", Now);

        Assert.True(position.Rename("Analista", "ANALISTA", Now.AddMinutes(1)));
        Assert.True(position.Deactivate(Now.AddMinutes(2)));
        Assert.False(position.Deactivate(Now.AddMinutes(3)));
        Assert.True(position.Reactivate(Now.AddMinutes(4)));
        Assert.Equal("Analista", position.Name);
        Assert.Equal(JobPositionStatus.Active, position.Status);
    }

    [Fact]
    public void RelationshipAndAssignmentCanOnlyEndOnce()
    {
        var organizationId = Guid.NewGuid();
        var relationship = EmploymentRelationship.Create(
            organizationId,
            Guid.NewGuid(),
            new DateOnly(2026, 8, 1),
            Now);
        var assignment = EmployeeAssignment.Create(
            organizationId,
            relationship.Id,
            Guid.NewGuid(),
            Guid.NewGuid(),
            true,
            new DateOnly(2026, 8, 1),
            Now);

        Assert.True(assignment.End(new DateOnly(2026, 8, 4), Now.AddMinutes(1)));
        Assert.False(assignment.End(new DateOnly(2026, 8, 5), Now.AddMinutes(2)));
        Assert.True(relationship.End(new DateOnly(2026, 8, 4), Now.AddMinutes(1)));
        Assert.False(relationship.End(new DateOnly(2026, 8, 5), Now.AddMinutes(2)));
    }
}
