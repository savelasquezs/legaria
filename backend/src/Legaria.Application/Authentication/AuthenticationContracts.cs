using Legaria.Domain.Authentication;

namespace Legaria.Application.Authentication;

public sealed record LoginRequest(string Email, string Password);
public sealed record EmailRequest(string Email);
public sealed record TokenRequest(string Token);
public sealed record ResetPasswordRequest(string Token, string NewPassword);

public sealed record AuthenticatedAccount(
    Guid Id,
    string AccountType,
    string Email,
    string FirstName,
    string LastName,
    IReadOnlyCollection<string> Roles,
    Guid? OrganizationId,
    Guid? EmployeeId);

public sealed record AuthenticationResult(
    string AccessToken,
    DateTimeOffset ExpiresAt,
    AuthenticatedAccount Account,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt);

public sealed record ClientContext(string? IpAddress, string? UserAgent);

public sealed record CurrentAccount(
    Guid UserId,
    AccountType AccountType,
    Guid? OrganizationId,
    Guid? EmployeeId,
    IReadOnlyCollection<string> Roles);

public static class AuthErrorCodes
{
    public const string InvalidCredentials = "auth.invalid_credentials";
    public const string AccountLocked = "auth.account_locked";
    public const string AccountUnavailable = "auth.account_unavailable";
    public const string EmailNotVerified = "auth.email_not_verified";
    public const string InvalidRefreshToken = "auth.invalid_refresh_token";
    public const string InvalidToken = "auth.token_invalid";
    public const string ExpiredToken = "auth.token_expired";
    public const string UsedToken = "auth.token_used";
    public const string InvalidPassword = "auth.invalid_password";
    public const string EmailDeliveryFailed = "email.delivery_failed";
    public const string UntrustedOrigin = "auth.untrusted_origin";
}

public sealed class AuthException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
