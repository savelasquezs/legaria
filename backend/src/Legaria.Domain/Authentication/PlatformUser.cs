namespace Legaria.Domain.Authentication;

public sealed class PlatformUser
{
    private PlatformUser()
    {
    }

    public Guid Id { get; private set; }
    public string Email { get; private set; } = string.Empty;
    public string NormalizedEmail { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public string FirstName { get; private set; } = string.Empty;
    public string LastName { get; private set; } = string.Empty;
    public PlatformRole Role { get; private set; }
    public AccountStatus Status { get; private set; }
    public DateTimeOffset? EmailVerifiedAt { get; private set; }
    public string SecurityStamp { get; private set; } = string.Empty;
    public DateTimeOffset? LastLoginAt { get; private set; }
    public int AccessFailedCount { get; private set; }
    public DateTimeOffset? LockoutEndAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public static PlatformUser CreateOwner(
        string email,
        string normalizedEmail,
        string passwordHash,
        string firstName,
        string lastName,
        string securityStamp,
        DateTimeOffset now)
    {
        return new PlatformUser
        {
            Id = Guid.NewGuid(),
            Email = email,
            NormalizedEmail = normalizedEmail,
            PasswordHash = passwordHash,
            FirstName = firstName,
            LastName = lastName,
            Role = PlatformRole.Owner,
            Status = AccountStatus.Active,
            EmailVerifiedAt = now,
            SecurityStamp = securityStamp,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    public bool IsLockedOut(DateTimeOffset now) => LockoutEndAt is not null && LockoutEndAt > now;

    public void RecordFailedLogin(DateTimeOffset now, int maximumAttempts, TimeSpan lockoutDuration)
    {
        AccessFailedCount++;
        if (AccessFailedCount >= maximumAttempts)
        {
            LockoutEndAt = now.Add(lockoutDuration);
            AccessFailedCount = 0;
        }

        UpdatedAt = now;
    }

    public void RecordSuccessfulLogin(DateTimeOffset now)
    {
        LastLoginAt = now;
        AccessFailedCount = 0;
        LockoutEndAt = null;
        UpdatedAt = now;
    }

    public void VerifyEmail(DateTimeOffset now)
    {
        EmailVerifiedAt ??= now;
        UpdatedAt = now;
    }

    public void ChangePassword(string passwordHash, string securityStamp, DateTimeOffset now)
    {
        PasswordHash = passwordHash;
        SecurityStamp = securityStamp;
        AccessFailedCount = 0;
        LockoutEndAt = null;
        UpdatedAt = now;
    }

    public void RotateSecurityStamp(string securityStamp, DateTimeOffset now)
    {
        SecurityStamp = securityStamp;
        UpdatedAt = now;
    }
}
