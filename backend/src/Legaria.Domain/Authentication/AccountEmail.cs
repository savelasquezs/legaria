namespace Legaria.Domain.Authentication;

public sealed class AccountEmail
{
    private AccountEmail()
    {
    }

    public string NormalizedEmail { get; private set; } = string.Empty;
    public AccountType AccountType { get; private set; }
    public Guid? PlatformUserId { get; private set; }
    public Guid? UserAccountId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public static AccountEmail ForPlatform(
        string normalizedEmail,
        Guid platformUserId,
        DateTimeOffset now) =>
        new()
        {
            NormalizedEmail = normalizedEmail,
            AccountType = AccountType.Platform,
            PlatformUserId = platformUserId,
            CreatedAt = now
        };

    public static AccountEmail ForTenant(
        string normalizedEmail,
        Guid userAccountId,
        DateTimeOffset now) =>
        new()
        {
            NormalizedEmail = normalizedEmail,
            AccountType = AccountType.Tenant,
            UserAccountId = userAccountId,
            CreatedAt = now
        };
}
