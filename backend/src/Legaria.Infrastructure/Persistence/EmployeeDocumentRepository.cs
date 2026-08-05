using Legaria.Application.Documents;
using Legaria.Domain.Documents;
using Microsoft.EntityFrameworkCore;

namespace Legaria.Infrastructure.Persistence;

public sealed class EmployeeDocumentRepository(LegariaDbContext dbContext) : IEmployeeDocumentRepository
{
    public async Task<IReadOnlyCollection<RequiredDocumentTypeQueryItem>> ListRequiredTypesAsync(
        Guid organizationId, Guid employeeId, IReadOnlyCollection<Guid>? visibleBranchIds, CancellationToken cancellationToken)
    {
        var hasActiveRelationship = await dbContext.EmploymentRelationships.AsNoTracking().AnyAsync(item =>
            item.OrganizationId == organizationId && item.EmployeeId == employeeId && item.EndedOn == null,
            cancellationToken);
        if (!hasActiveRelationship) return [];

        var activePositionIds = from relationship in dbContext.EmploymentRelationships.AsNoTracking()
            join assignment in dbContext.EmployeeAssignments.AsNoTracking()
                on new { relationship.OrganizationId, Id = relationship.Id }
                equals new { assignment.OrganizationId, Id = assignment.EmploymentRelationshipId }
            where relationship.OrganizationId == organizationId && relationship.EmployeeId == employeeId &&
                relationship.EndedOn == null && assignment.EndedOn == null &&
                (visibleBranchIds == null || visibleBranchIds.Contains(assignment.BranchId))
            select assignment.JobPositionId;

        var explicitTypeIds = from requirement in dbContext.JobPositionDocumentRequirements.AsNoTracking()
            where requirement.OrganizationId == organizationId && activePositionIds.Contains(requirement.JobPositionId)
            select requirement.DocumentTypeId;

        return await (from documentType in dbContext.DocumentTypes.AsNoTracking()
            join category in dbContext.DocumentCategories.AsNoTracking()
                on new { documentType.OrganizationId, Id = documentType.CategoryId }
                equals new { category.OrganizationId, category.Id }
            where documentType.OrganizationId == organizationId && documentType.Status == DocumentCatalogStatus.Active &&
                category.Status == DocumentCatalogStatus.Active && category.Scope == DocumentScope.Employee &&
                (documentType.IsRequiredByDefault || explicitTypeIds.Contains(documentType.Id))
            orderby category.Name, documentType.Name
            select new RequiredDocumentTypeQueryItem(documentType, category)).ToArrayAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<EmployeeDocumentQueryItem>> ListCurrentDocumentsAsync(
        Guid organizationId, Guid employeeId, CancellationToken cancellationToken) =>
        await (from document in dbContext.EmployeeDocuments.AsNoTracking()
            join documentType in dbContext.DocumentTypes.AsNoTracking()
                on new { document.OrganizationId, Id = document.DocumentTypeId }
                equals new { documentType.OrganizationId, documentType.Id }
            join category in dbContext.DocumentCategories.AsNoTracking()
                on new { documentType.OrganizationId, Id = documentType.CategoryId }
                equals new { category.OrganizationId, category.Id }
            where document.OrganizationId == organizationId && document.EmployeeId == employeeId && document.ReplacedAt == null
            select new EmployeeDocumentQueryItem(document, documentType, category)).ToArrayAsync(cancellationToken);

    public async Task<IReadOnlyCollection<EmployeeDocument>> ListCurrentVersionsAsync(
        Guid organizationId, Guid employeeId, Guid documentTypeId, CancellationToken cancellationToken) =>
        await dbContext.EmployeeDocuments.Where(item => item.OrganizationId == organizationId && item.EmployeeId == employeeId &&
            item.DocumentTypeId == documentTypeId && item.ReplacedAt == null).ToArrayAsync(cancellationToken);

    public async Task<EmployeeDocumentEvidenceQueryItem?> FindEvidenceAsync(
        Guid organizationId, Guid employeeId, Guid evidenceId, CancellationToken cancellationToken) =>
        await (from evidence in dbContext.EmployeeDocumentEvidences.AsNoTracking()
            join document in dbContext.EmployeeDocuments.AsNoTracking()
                on new { evidence.OrganizationId, Id = evidence.EmployeeDocumentId }
                equals new { document.OrganizationId, document.Id }
            where evidence.OrganizationId == organizationId && evidence.Id == evidenceId && document.EmployeeId == employeeId
            select new EmployeeDocumentEvidenceQueryItem(evidence, document)).SingleOrDefaultAsync(cancellationToken);

    public void AddDocument(EmployeeDocument document) => dbContext.EmployeeDocuments.Add(document);
    public void AddEvidences(IEnumerable<EmployeeDocumentEvidence> evidences) => dbContext.EmployeeDocumentEvidences.AddRange(evidences);
    public Task SaveChangesAsync(CancellationToken cancellationToken) => dbContext.SaveChangesAsync(cancellationToken);
}
