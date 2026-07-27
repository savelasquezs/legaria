namespace Legaria.Domain.Authentication;

public sealed class RefreshSession
{
    private RefreshSession()
    {
    }

    public Guid Id { get; private set; }
    public Guid? PlatformUserId { get; private set; }
    public Guid? UserAccountId { get; private set; }
    public Guid FamilyId { get; private set; }
    public string TokenHash { get; private set; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public string? CreatedByIp { get; private set; }
    public string? UserAgent { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }
    public string? RevokedByIp { get; private set; }
    public string? RevocationReason { get; private set; }
    public Guid? ReplacedBySessionId { get; private set; }

    public bool IsActive(DateTimeOffset now) => RevokedAt is null && ExpiresAt > now;

    public static RefreshSession Create(
        Guid? platformUserId,
        Guid? userAccountId,
        Guid familyId,
        string tokenHash,
        DateTimeOffset expiresAt,
        DateTimeOffset now,
        string? ipAddress,
        string? userAgent)
    {
        return new RefreshSession
        {
            Id = Guid.NewGuid(),
            PlatformUserId = platformUserId,
            UserAccountId = userAccountId,
            FamilyId = familyId,
            TokenHash = tokenHash,
            ExpiresAt = expiresAt,
            CreatedAt = now,
            CreatedByIp = ipAddress,
            UserAgent = userAgent
        };
    }

    public void Revoke(DateTimeOffset now, string? ipAddress, string reason, Guid? replacementId = null)
    {
        if (RevokedAt is not null)
        {
            return;
        }

        RevokedAt = now;
        RevokedByIp = ipAddress;
        RevocationReason = reason;
        ReplacedBySessionId = replacementId;
    }
}
