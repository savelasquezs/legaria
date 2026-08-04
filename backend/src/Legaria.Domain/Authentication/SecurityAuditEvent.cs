namespace Legaria.Domain.Authentication;

public sealed class SecurityAuditEvent
{
    private SecurityAuditEvent()
    {
    }

    public Guid Id { get; private set; }
    public string EventType { get; private set; } = string.Empty;
    public AccountType? AccountType { get; private set; }
    public Guid? PlatformUserId { get; private set; }
    public Guid? UserAccountId { get; private set; }
    public Guid? OrganizationId { get; private set; }
    public string Outcome { get; private set; } = string.Empty;
    public string? IpAddress { get; private set; }
    public string? UserAgent { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public static SecurityAuditEvent Create(
        string eventType,
        string outcome,
        DateTimeOffset now,
        AccountType? accountType = null,
        Guid? platformUserId = null,
        Guid? userAccountId = null,
        string? ipAddress = null,
        string? userAgent = null,
        Guid? organizationId = null)
    {
        return new SecurityAuditEvent
        {
            Id = Guid.NewGuid(),
            EventType = eventType,
            AccountType = accountType,
            PlatformUserId = platformUserId,
            UserAccountId = userAccountId,
            OrganizationId = organizationId,
            Outcome = outcome,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            CreatedAt = now
        };
    }
}
