namespace Legaria.Domain.Employees;

public sealed class JobPositionDocumentRequirement
{
    private JobPositionDocumentRequirement()
    {
    }

    public Guid OrganizationId { get; private set; }
    public Guid JobPositionId { get; private set; }
    public Guid DocumentTypeId { get; private set; }

    public static JobPositionDocumentRequirement Create(
        Guid organizationId,
        Guid jobPositionId,
        Guid documentTypeId) =>
        new()
        {
            OrganizationId = organizationId,
            JobPositionId = jobPositionId,
            DocumentTypeId = documentTypeId
        };
}
