using Legaria.Application.Authentication;
using Legaria.Domain.Authentication;
using Legaria.Domain.Tenancy;

namespace Legaria.Application.Branches;

public sealed record BranchQueryItem(Branch Branch, Municipality Municipality, Department Department);
public sealed record BranchAdministratorQueryItem(
    UserAccount Account,
    AccountToken? Invitation,
    IReadOnlyCollection<Branch> Branches);

public interface IBranchTransaction : IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken);
}

public interface IBranchRepository
{
    Task<(IReadOnlyCollection<BranchQueryItem> Items, int Total)> ListBranchesAsync(
        Guid organizationId,
        Guid? assignedUserAccountId,
        int skip,
        int take,
        string? search,
        BranchStatus? status,
        CancellationToken cancellationToken);
    Task<BranchQueryItem?> FindBranchDetailsAsync(
        Guid organizationId,
        Guid branchId,
        Guid? assignedUserAccountId,
        CancellationToken cancellationToken);
    Task<Branch?> FindBranchAsync(Guid organizationId, Guid branchId, CancellationToken cancellationToken);
    Task<Municipality?> FindMunicipalityAsync(string code, CancellationToken cancellationToken);
    Task<bool> BranchNameExistsAsync(
        Guid organizationId,
        string normalizedName,
        Guid? excludingBranchId,
        CancellationToken cancellationToken);
    Task<IReadOnlyCollection<Branch>> FindActiveBranchesAsync(
        Guid organizationId,
        IReadOnlyCollection<Guid> branchIds,
        CancellationToken cancellationToken);
    Task<(IReadOnlyCollection<BranchAdministratorQueryItem> Items, int Total)> ListAdministratorsAsync(
        Guid organizationId,
        int skip,
        int take,
        string? search,
        AccountStatus? status,
        CancellationToken cancellationToken);
    Task<BranchAdministratorQueryItem?> FindAdministratorDetailsAsync(
        Guid organizationId,
        Guid accountId,
        CancellationToken cancellationToken);
    Task<UserAccount?> FindAdministratorAsync(
        Guid organizationId,
        Guid accountId,
        CancellationToken cancellationToken);
    Task<AccountEmail?> FindAccountEmailAsync(Guid accountId, CancellationToken cancellationToken);
    Task<bool> EmailExistsAsync(string normalizedEmail, Guid? excludingAccountId, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<UserBranchAccess>> FindActiveAccessesAsync(
        Guid organizationId,
        Guid accountId,
        CancellationToken cancellationToken);
    Task<IReadOnlyCollection<RefreshSession>> FindActiveSessionsAsync(
        Guid accountId,
        DateTimeOffset now,
        CancellationToken cancellationToken);
    Task<IReadOnlyCollection<AccountToken>> FindActiveInvitationsAsync(
        Guid accountId,
        CancellationToken cancellationToken);
    Task<Organization?> FindOrganizationAsync(Guid organizationId, CancellationToken cancellationToken);
    Task<Organization?> FindOrganizationForUpdateAsync(Guid organizationId, CancellationToken cancellationToken);
    Task<bool> OrganizationHasBranchesAsync(Guid organizationId, CancellationToken cancellationToken);
    Task<IBranchTransaction> BeginTransactionAsync(CancellationToken cancellationToken);
    void AddBranch(Branch branch);
    void AddUserAccount(UserAccount account);
    void AddAccountEmail(AccountEmail accountEmail);
    void RemoveAccountEmail(AccountEmail accountEmail);
    void AddAccess(UserBranchAccess access);
    void AddAuditEvent(SecurityAuditEvent auditEvent);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public interface IBranchService
{
    Task<BranchPage> ListBranchesAsync(
        int page,
        int pageSize,
        string? search,
        string? status,
        CurrentAccount actor,
        CancellationToken cancellationToken);
    Task<BranchResult> GetBranchAsync(Guid id, CurrentAccount actor, CancellationToken cancellationToken);
    Task<BranchResult> CreateBranchAsync(
        BranchInput input,
        CurrentAccount actor,
        ClientContext client,
        CancellationToken cancellationToken);
    Task<BranchResult> CreateInitialBranchAsync(
        Guid organizationId,
        BranchInput input,
        CurrentAccount actor,
        ClientContext client,
        CancellationToken cancellationToken);
    Task<BranchResult> UpdateBranchAsync(
        Guid id,
        BranchInput input,
        CurrentAccount actor,
        ClientContext client,
        CancellationToken cancellationToken);
    Task<BranchResult> DeactivateBranchAsync(
        Guid id,
        CurrentAccount actor,
        ClientContext client,
        CancellationToken cancellationToken);
    Task<BranchResult> ReactivateBranchAsync(
        Guid id,
        CurrentAccount actor,
        ClientContext client,
        CancellationToken cancellationToken);
    Task<BranchAdministratorPage> ListAdministratorsAsync(
        int page,
        int pageSize,
        string? search,
        string? status,
        CurrentAccount actor,
        CancellationToken cancellationToken);
    Task<BranchAdministratorResult> GetAdministratorAsync(
        Guid id,
        CurrentAccount actor,
        CancellationToken cancellationToken);
    Task<BranchAdministratorResult> UpdatePendingAdministratorAsync(
        Guid id,
        BranchAdministratorInput input,
        CurrentAccount actor,
        ClientContext client,
        CancellationToken cancellationToken);
    Task<BranchAdministratorResult> UpdateAssignmentsAsync(
        Guid id,
        UpdateBranchAssignmentsRequest request,
        CurrentAccount actor,
        ClientContext client,
        CancellationToken cancellationToken);
    Task<BranchAdministratorResult> ResendInvitationAsync(
        Guid id,
        CurrentAccount actor,
        ClientContext client,
        CancellationToken cancellationToken);
    Task<BranchAdministratorResult> SuspendAdministratorAsync(
        Guid id,
        CurrentAccount actor,
        ClientContext client,
        CancellationToken cancellationToken);
    Task<BranchAdministratorResult> ReactivateAdministratorAsync(
        Guid id,
        CurrentAccount actor,
        ClientContext client,
        CancellationToken cancellationToken);
}
