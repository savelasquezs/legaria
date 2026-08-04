namespace Legaria.Domain.Authentication;

public sealed class AccountToken
{
    private AccountToken()
    {
    }

    public Guid Id { get; private set; }
    public AccountType AccountType { get; private set; }
    public Guid? PlatformUserId { get; private set; }
    public Guid? UserAccountId { get; private set; }
    public AccountTokenPurpose Purpose { get; private set; }
    public string TokenHash { get; private set; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? UsedAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }
    public DateTimeOffset? DeliveredAt { get; private set; }
    public DateTimeOffset? DeliveryFailedAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public string? CreatedByIp { get; private set; }

    public static AccountToken Create(
        AccountType accountType,
        Guid? platformUserId,
        Guid? userAccountId,
        AccountTokenPurpose purpose,
        string tokenHash,
        DateTimeOffset expiresAt,
        DateTimeOffset now,
        string? ipAddress)
    {
        return new AccountToken
        {
            Id = Guid.NewGuid(),
            AccountType = accountType,
            PlatformUserId = platformUserId,
            UserAccountId = userAccountId,
            Purpose = purpose,
            TokenHash = tokenHash,
            ExpiresAt = expiresAt,
            CreatedAt = now,
            CreatedByIp = ipAddress
        };
    }

    public bool IsUsable(DateTimeOffset now) =>
        UsedAt is null && RevokedAt is null && ExpiresAt > now;

    public void MarkUsed(DateTimeOffset now) => UsedAt = now;

    public void MarkDelivered(DateTimeOffset now)
    {
        DeliveredAt = now;
        DeliveryFailedAt = null;
    }

    public void MarkDeliveryFailed(DateTimeOffset now)
    {
        DeliveredAt = null;
        DeliveryFailedAt = now;
    }

    public void Revoke(DateTimeOffset now)
    {
        if (UsedAt is null && RevokedAt is null)
        {
            RevokedAt = now;
        }
    }
}
