using Legaria.Domain.Authentication;
using Legaria.Domain.Tenancy;

namespace Legaria.Application.Organizations;

public sealed record OrganizationQueryItem(
    Organization Organization,
    Municipality Municipality,
    Department Department,
    UserAccount InitialAdmin,
    AccountToken? Invitation);

public interface IOrganizationRepository
{
    Task<(IReadOnlyCollection<OrganizationQueryItem> Items, int Total)> ListAsync(
        int skip,
        int take,
        string? search,
        OrganizationStatus? status,
        DateTimeOffset now,
        CancellationToken cancellationToken);
    Task<OrganizationQueryItem?> FindDetailsAsync(Guid id, CancellationToken cancellationToken);
    Task<Organization?> FindOrganizationAsync(Guid id, CancellationToken cancellationToken);
    Task<UserAccount?> FindInitialAdminAsync(Guid organizationId, CancellationToken cancellationToken);
    Task<UserAccount?> FindUserAccountAsync(Guid id, CancellationToken cancellationToken);
    Task<AccountEmail?> FindAccountEmailForUserAsync(Guid userAccountId, CancellationToken cancellationToken);
    Task<AccountToken?> FindLatestInvitationAsync(Guid userAccountId, CancellationToken cancellationToken);
    Task<Municipality?> FindMunicipalityAsync(string code, CancellationToken cancellationToken);
    Task<bool> NitExistsAsync(string nit, Guid? excludingOrganizationId, CancellationToken cancellationToken);
    Task<bool> EmailExistsAsync(string normalizedEmail, Guid? excludingUserAccountId, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<Department>> GetDepartmentsAsync(CancellationToken cancellationToken);
    Task<IReadOnlyCollection<Municipality>> GetMunicipalitiesAsync(string departmentCode, CancellationToken cancellationToken);
    void AddOrganization(Organization organization);
    void AddUserAccount(UserAccount account);
    void AddAccountEmail(AccountEmail accountEmail);
    void RemoveAccountEmail(AccountEmail accountEmail);
    void AddAuditEvent(SecurityAuditEvent auditEvent);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public interface INitValidator
{
    bool IsValid(string nit, int verificationDigit);
    int CalculateVerificationDigit(string nit);
}
