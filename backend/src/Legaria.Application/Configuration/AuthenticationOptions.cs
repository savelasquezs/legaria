namespace Legaria.Application.Configuration;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";
    public string Issuer { get; init; } = string.Empty;
    public string Audience { get; init; } = string.Empty;
    public string SigningKey { get; init; } = string.Empty;
    public int AccessTokenMinutes { get; init; } = 10;
}

public sealed class ResendOptions
{
    public const string SectionName = "Resend";
    public string ApiKey { get; init; } = string.Empty;
    public string FromEmail { get; init; } = string.Empty;
    public string FromName { get; init; } = string.Empty;
    public string? ReplyToEmail { get; init; }
}

public sealed class FrontendOptions
{
    public const string SectionName = "Frontend";
    public string BaseUrl { get; init; } = string.Empty;
}

public sealed class BootstrapOwnerOptions
{
    public const string SectionName = "BootstrapOwner";
    public string Email { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
}

public sealed class AuthenticationOptions
{
    public const string SectionName = "Authentication";
    public int RefreshTokenDays { get; init; } = 7;
    public int MaximumFailedAttempts { get; init; } = 5;
    public int LockoutMinutes { get; init; } = 15;
    public int VerificationTokenHours { get; init; } = 24;
    public int PasswordResetTokenMinutes { get; init; } = 30;
}
