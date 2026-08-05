using Legaria.Domain.Authentication;

namespace Legaria.Application.Authentication;

public interface IAuthenticationService
{
    Task<AuthenticationResult> LoginAsync(LoginRequest request, ClientContext client, CancellationToken cancellationToken);
    Task<AuthenticationResult> RefreshAsync(string refreshToken, ClientContext client, CancellationToken cancellationToken);
    Task LogoutAsync(string? refreshToken, ClientContext client, CancellationToken cancellationToken);
    Task LogoutAllAsync(CurrentAccount account, ClientContext client, CancellationToken cancellationToken);
    Task<AuthenticatedAccount> GetCurrentAsync(CurrentAccount account, CancellationToken cancellationToken);
    Task VerifyEmailAsync(string token, ClientContext client, CancellationToken cancellationToken);
    Task RequestEmailVerificationAsync(string email, ClientContext client, CancellationToken cancellationToken);
    Task RequestPasswordResetAsync(string email, ClientContext client, CancellationToken cancellationToken);
    Task ResetPasswordAsync(ResetPasswordRequest request, ClientContext client, CancellationToken cancellationToken);
}

public interface IAuthenticationRepository
{
    Task<bool> AnyPlatformUserAsync(CancellationToken cancellationToken);
    Task<PlatformUser?> FindPlatformByEmailAsync(string normalizedEmail, CancellationToken cancellationToken);
    Task<UserAccount?> FindTenantByEmailAsync(string normalizedEmail, CancellationToken cancellationToken);
    Task<PlatformUser?> FindPlatformByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<UserAccount?> FindTenantByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<bool> IsOrganizationActiveAsync(Guid organizationId, CancellationToken cancellationToken);
    Task<bool> EmailExistsAsync(string normalizedEmail, CancellationToken cancellationToken);
    Task<RefreshSession?> FindRefreshSessionAsync(string tokenHash, CancellationToken cancellationToken);
    Task<AccountToken?> FindAccountTokenAsync(string tokenHash, AccountTokenPurpose purpose, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<AccountToken>> FindActiveAccountTokensAsync(
        AccountType accountType,
        Guid accountId,
        AccountTokenPurpose purpose,
        CancellationToken cancellationToken);
    Task<IReadOnlyCollection<RefreshSession>> FindSessionsByFamilyAsync(Guid familyId, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<RefreshSession>> FindActiveSessionsAsync(
        AccountType accountType,
        Guid accountId,
        DateTimeOffset now,
        CancellationToken cancellationToken);
    void AddPlatformUser(PlatformUser platformUser);
    void AddAccountEmail(AccountEmail accountEmail);
    void AddRefreshSession(RefreshSession refreshSession);
    void AddAccountToken(AccountToken accountToken);
    void AddAuditEvent(SecurityAuditEvent auditEvent);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public interface IPasswordService
{
    string Hash(string password);
    bool Verify(string passwordHash, string password);
    void VerifyUnknown(string password);
}

public interface IEmailNormalizer
{
    string Normalize(string email);
}

public interface ISecureTokenService
{
    string GenerateToken();
    string HashToken(string token);
    string GenerateSecurityStamp();
}

public sealed record AccessToken(string Value, DateTimeOffset ExpiresAt);

public interface IAccessTokenService
{
    AccessToken Create(
        Guid userId,
        AccountType accountType,
        string securityStamp,
        IReadOnlyCollection<string> roles,
        Guid? organizationId,
        Guid? employeeId);
}

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}

public sealed record EmailMessage(
    string Recipient,
    string Subject,
    string HtmlBody,
    string? TextBody = null);

public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
}

public interface IEmailTemplateRenderer
{
    string RenderVerification(string firstName, string verificationUrl, TimeSpan expiration);
    string RenderPasswordReset(string firstName, string resetUrl, TimeSpan expiration);
    string RenderTenantInvitation(
        string firstName,
        string organizationName,
        string accessProfile,
        string invitationUrl,
        TimeSpan expiration);
}

public interface IPlatformOwnerBootstrapper
{
    Task BootstrapAsync(CancellationToken cancellationToken);
}

public interface ICurrentUser
{
    bool IsAuthenticated { get; }
    Guid UserId { get; }
    AccountType AccountType { get; }
    bool IsPlatformUser { get; }
    bool IsTenantUser { get; }
    Guid? OrganizationId { get; }
    Guid? EmployeeId { get; }
    IReadOnlyCollection<string> Roles { get; }
    CurrentAccount ToCurrentAccount();
}

public sealed class EmailDeliveryException(string message, Exception? innerException = null)
    : Exception(message, innerException);
