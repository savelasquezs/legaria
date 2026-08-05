namespace Legaria.Domain.Authentication;

public sealed class UserBranchAccess
{
    private UserBranchAccess()
    {
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid UserAccountId { get; private set; }
    public Guid BranchId { get; private set; }
    public DateTimeOffset GrantedAt { get; private set; }
    public Guid GrantedByUserAccountId { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }
    public Guid? RevokedByUserAccountId { get; private set; }

    public static UserBranchAccess Grant(
        Guid organizationId,
        Guid userAccountId,
        Guid branchId,
        Guid actorUserAccountId,
        DateTimeOffset now) =>
        new()
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            UserAccountId = userAccountId,
            BranchId = branchId,
            GrantedAt = now,
            GrantedByUserAccountId = actorUserAccountId
        };

    public void Revoke(Guid actorUserAccountId, DateTimeOffset now)
    {
        if (RevokedAt is not null)
        {
            return;
        }

        RevokedAt = now;
        RevokedByUserAccountId = actorUserAccountId;
    }
}
