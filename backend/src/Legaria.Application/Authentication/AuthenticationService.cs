using Legaria.Application.Configuration;
using Legaria.Domain.Authentication;

namespace Legaria.Application.Authentication;

public sealed class AuthenticationService(
    IAuthenticationRepository repository,
    IPasswordService passwordService,
    IEmailNormalizer emailNormalizer,
    ISecureTokenService secureTokenService,
    IAccessTokenService accessTokenService,
    IEmailSender emailSender,
    IEmailTemplateRenderer templateRenderer,
    IClock clock,
    AuthenticationOptions authenticationOptions,
    FrontendOptions frontendOptions) : IAuthenticationService
{
    private const string GenericRecoveryMessage =
        "Si existe una cuenta asociada al correo, recibirás las instrucciones.";

    public async Task<AuthenticationResult> LoginAsync(
        LoginRequest request,
        ClientContext client,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrEmpty(request.Password) ||
            request.Password.Length > 128)
        {
            throw InvalidCredentials();
        }

        var normalizedEmail = emailNormalizer.Normalize(request.Email);
        var platformUser = await repository.FindPlatformByEmailAsync(normalizedEmail, cancellationToken);
        if (platformUser is not null)
        {
            return await LoginPlatformAsync(platformUser, request.Password, client, cancellationToken);
        }

        var tenantUser = await repository.FindTenantByEmailAsync(normalizedEmail, cancellationToken);
        if (tenantUser is not null)
        {
            return await LoginTenantAsync(tenantUser, request.Password, client, cancellationToken);
        }

        passwordService.VerifyUnknown(request.Password);
        throw InvalidCredentials();
    }

    public async Task<AuthenticationResult> RefreshAsync(
        string refreshToken,
        ClientContext client,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            throw new AuthException(AuthErrorCodes.InvalidRefreshToken, "La sesión no es válida.");
        }

        var now = clock.UtcNow;
        var tokenHash = secureTokenService.HashToken(refreshToken);
        var session = await repository.FindRefreshSessionAsync(tokenHash, cancellationToken)
            ?? throw new AuthException(AuthErrorCodes.InvalidRefreshToken, "La sesión no es válida.");

        if (session.RevokedAt is not null)
        {
            await RevokeFamilyAsync(session.FamilyId, client, "TOKEN_REUSE_DETECTED", cancellationToken);
            repository.AddAuditEvent(CreateAudit(
                "REFRESH_TOKEN_REUSE_DETECTED",
                "REJECTED",
                session.PlatformUserId is not null ? AccountType.Platform : AccountType.Tenant,
                session.PlatformUserId,
                session.UserAccountId,
                client));
            await repository.SaveChangesAsync(cancellationToken);
            throw new AuthException(AuthErrorCodes.InvalidRefreshToken, "La sesión no es válida.");
        }

        if (session.ExpiresAt <= now)
        {
            session.Revoke(now, client.IpAddress, "EXPIRED");
            await repository.SaveChangesAsync(cancellationToken);
            throw new AuthException(AuthErrorCodes.InvalidRefreshToken, "La sesión expiró.");
        }

        var account = await LoadAccountAsync(session, cancellationToken);
        var newRawToken = secureTokenService.GenerateToken();
        var replacement = RefreshSession.Create(
            session.PlatformUserId,
            session.UserAccountId,
            session.FamilyId,
            secureTokenService.HashToken(newRawToken),
            now.AddDays(authenticationOptions.RefreshTokenDays),
            now,
            client.IpAddress,
            client.UserAgent);
        repository.AddRefreshSession(replacement);
        session.Revoke(now, client.IpAddress, "ROTATED", replacement.Id);
        await repository.SaveChangesAsync(cancellationToken);

        var accessToken = accessTokenService.Create(
            account.Id,
            account.AccountType == AccountTypeCodes.Platform ? AccountType.Platform : AccountType.Tenant,
            await GetSecurityStampAsync(account, cancellationToken),
            account.Roles,
            account.OrganizationId,
            account.EmployeeId);

        return new AuthenticationResult(
            accessToken.Value,
            accessToken.ExpiresAt,
            account,
            newRawToken,
            replacement.ExpiresAt);
    }

    public async Task LogoutAsync(
        string? refreshToken,
        ClientContext client,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return;
        }

        var session = await repository.FindRefreshSessionAsync(
            secureTokenService.HashToken(refreshToken),
            cancellationToken);
        if (session is null || session.RevokedAt is not null)
        {
            return;
        }

        session.Revoke(clock.UtcNow, client.IpAddress, "LOGOUT");
        repository.AddAuditEvent(CreateAudit(
            "LOGOUT",
            "SUCCESS",
            session.PlatformUserId is not null ? AccountType.Platform : AccountType.Tenant,
            session.PlatformUserId,
            session.UserAccountId,
            client));
        await repository.SaveChangesAsync(cancellationToken);
    }

    public async Task LogoutAllAsync(
        CurrentAccount account,
        ClientContext client,
        CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var sessions = await repository.FindActiveSessionsAsync(
            account.AccountType,
            account.UserId,
            now,
            cancellationToken);
        foreach (var session in sessions)
        {
            session.Revoke(now, client.IpAddress, "LOGOUT_ALL");
        }

        if (account.AccountType == AccountType.Platform)
        {
            var platformUser = await repository.FindPlatformByIdAsync(account.UserId, cancellationToken)
                ?? throw new AuthException(AuthErrorCodes.AccountUnavailable, "La cuenta no está disponible.");
            platformUser.RotateSecurityStamp(secureTokenService.GenerateSecurityStamp(), now);
        }
        else
        {
            var tenantUser = await repository.FindTenantByIdAsync(account.UserId, cancellationToken)
                ?? throw new AuthException(AuthErrorCodes.AccountUnavailable, "La cuenta no está disponible.");
            tenantUser.RotateSecurityStamp(secureTokenService.GenerateSecurityStamp(), now);
        }

        repository.AddAuditEvent(CreateAudit(
            "LOGOUT_ALL",
            "SUCCESS",
            account.AccountType,
            account.AccountType == AccountType.Platform ? account.UserId : null,
            account.AccountType == AccountType.Tenant ? account.UserId : null,
            client));
        await repository.SaveChangesAsync(cancellationToken);
    }

    public async Task<AuthenticatedAccount> GetCurrentAsync(
        CurrentAccount account,
        CancellationToken cancellationToken)
    {
        if (account.AccountType == AccountType.Platform)
        {
            var user = await repository.FindPlatformByIdAsync(account.UserId, cancellationToken)
                ?? throw new AuthException(AuthErrorCodes.AccountUnavailable, "La cuenta no está disponible.");
            EnsurePlatformAvailable(user, clock.UtcNow);
            return Map(user);
        }

        var tenantUser = await repository.FindTenantByIdAsync(account.UserId, cancellationToken)
            ?? throw new AuthException(AuthErrorCodes.AccountUnavailable, "La cuenta no está disponible.");
        await EnsureTenantAvailableAsync(tenantUser, clock.UtcNow, cancellationToken);
        return Map(tenantUser);
    }

    public async Task VerifyEmailAsync(
        string token,
        ClientContext client,
        CancellationToken cancellationToken)
    {
        var accountToken = await GetUsableAccountTokenAsync(
            token,
            AccountTokenPurpose.EmailVerification,
            cancellationToken);
        var now = clock.UtcNow;

        if (accountToken.AccountType == AccountType.Platform)
        {
            var user = await repository.FindPlatformByIdAsync(accountToken.PlatformUserId!.Value, cancellationToken)
                ?? throw InvalidToken();
            user.VerifyEmail(now);
        }
        else
        {
            var user = await repository.FindTenantByIdAsync(accountToken.UserAccountId!.Value, cancellationToken)
                ?? throw InvalidToken();
            user.VerifyEmail(now);
        }

        accountToken.MarkUsed(now);
        repository.AddAuditEvent(CreateAudit(
            "EMAIL_VERIFIED",
            "SUCCESS",
            accountToken.AccountType,
            accountToken.PlatformUserId,
            accountToken.UserAccountId,
            client));
        await repository.SaveChangesAsync(cancellationToken);
    }

    public Task RequestEmailVerificationAsync(
        string email,
        ClientContext client,
        CancellationToken cancellationToken) =>
        RequestAccountEmailAsync(
            email,
            AccountTokenPurpose.EmailVerification,
            client,
            cancellationToken);

    public Task RequestPasswordResetAsync(
        string email,
        ClientContext client,
        CancellationToken cancellationToken) =>
        RequestAccountEmailAsync(
            email,
            AccountTokenPurpose.PasswordReset,
            client,
            cancellationToken);

    public async Task ResetPasswordAsync(
        ResetPasswordRequest request,
        ClientContext client,
        CancellationToken cancellationToken)
    {
        ValidatePassword(request.NewPassword);
        var accountToken = await GetUsableAccountTokenAsync(
            request.Token,
            AccountTokenPurpose.PasswordReset,
            cancellationToken);
        var now = clock.UtcNow;
        var passwordHash = passwordService.Hash(request.NewPassword);
        var securityStamp = secureTokenService.GenerateSecurityStamp();
        var accountId = accountToken.PlatformUserId ?? accountToken.UserAccountId!.Value;

        if (accountToken.AccountType == AccountType.Platform)
        {
            var user = await repository.FindPlatformByIdAsync(accountId, cancellationToken)
                ?? throw InvalidToken();
            user.ChangePassword(passwordHash, securityStamp, now);
        }
        else
        {
            var user = await repository.FindTenantByIdAsync(accountId, cancellationToken)
                ?? throw InvalidToken();
            user.ChangePassword(passwordHash, securityStamp, now);
        }

        var sessions = await repository.FindActiveSessionsAsync(
            accountToken.AccountType,
            accountId,
            now,
            cancellationToken);
        foreach (var session in sessions)
        {
            session.Revoke(now, client.IpAddress, "PASSWORD_RESET");
        }

        accountToken.MarkUsed(now);
        repository.AddAuditEvent(CreateAudit(
            "PASSWORD_RESET_COMPLETED",
            "SUCCESS",
            accountToken.AccountType,
            accountToken.PlatformUserId,
            accountToken.UserAccountId,
            client));
        await repository.SaveChangesAsync(cancellationToken);
    }

    public static string GetGenericRecoveryMessage() => GenericRecoveryMessage;

    private async Task<AuthenticationResult> LoginPlatformAsync(
        PlatformUser user,
        string password,
        ClientContext client,
        CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        if (user.IsLockedOut(now))
        {
            throw new AuthException(AuthErrorCodes.AccountLocked, "La cuenta está bloqueada temporalmente.");
        }

        if (!passwordService.Verify(user.PasswordHash, password))
        {
            user.RecordFailedLogin(
                now,
                authenticationOptions.MaximumFailedAttempts,
                TimeSpan.FromMinutes(authenticationOptions.LockoutMinutes));
            repository.AddAuditEvent(CreateAudit(
                "LOGIN_FAILED",
                "INVALID_CREDENTIALS",
                AccountType.Platform,
                user.Id,
                null,
                client));
            await repository.SaveChangesAsync(cancellationToken);
            throw InvalidCredentials();
        }

        EnsurePlatformAvailable(user, now);
        user.RecordSuccessfulLogin(now);
        return await CreateLoginResultAsync(user, client, cancellationToken);
    }

    private async Task<AuthenticationResult> LoginTenantAsync(
        UserAccount user,
        string password,
        ClientContext client,
        CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        if (user.IsLockedOut(now))
        {
            throw new AuthException(AuthErrorCodes.AccountLocked, "La cuenta está bloqueada temporalmente.");
        }

        if (!passwordService.Verify(user.PasswordHash, password))
        {
            user.RecordFailedLogin(
                now,
                authenticationOptions.MaximumFailedAttempts,
                TimeSpan.FromMinutes(authenticationOptions.LockoutMinutes));
            repository.AddAuditEvent(CreateAudit(
                "LOGIN_FAILED",
                "INVALID_CREDENTIALS",
                AccountType.Tenant,
                null,
                user.Id,
                client));
            await repository.SaveChangesAsync(cancellationToken);
            throw InvalidCredentials();
        }

        await EnsureTenantAvailableAsync(user, now, cancellationToken);
        user.RecordSuccessfulLogin(now);
        return await CreateLoginResultAsync(user, client, cancellationToken);
    }

    private async Task<AuthenticationResult> CreateLoginResultAsync(
        PlatformUser user,
        ClientContext client,
        CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var rawRefreshToken = secureTokenService.GenerateToken();
        var session = RefreshSession.Create(
            user.Id,
            null,
            Guid.NewGuid(),
            secureTokenService.HashToken(rawRefreshToken),
            now.AddDays(authenticationOptions.RefreshTokenDays),
            now,
            client.IpAddress,
            client.UserAgent);
        repository.AddRefreshSession(session);
        repository.AddAuditEvent(CreateAudit(
            "LOGIN_SUCCEEDED",
            "SUCCESS",
            AccountType.Platform,
            user.Id,
            null,
            client));
        await repository.SaveChangesAsync(cancellationToken);
        var account = Map(user);
        var accessToken = accessTokenService.Create(
            user.Id,
            AccountType.Platform,
            user.SecurityStamp,
            account.Roles,
            null,
            null);
        return new AuthenticationResult(
            accessToken.Value,
            accessToken.ExpiresAt,
            account,
            rawRefreshToken,
            session.ExpiresAt);
    }

    private async Task<AuthenticationResult> CreateLoginResultAsync(
        UserAccount user,
        ClientContext client,
        CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var rawRefreshToken = secureTokenService.GenerateToken();
        var session = RefreshSession.Create(
            null,
            user.Id,
            Guid.NewGuid(),
            secureTokenService.HashToken(rawRefreshToken),
            now.AddDays(authenticationOptions.RefreshTokenDays),
            now,
            client.IpAddress,
            client.UserAgent);
        repository.AddRefreshSession(session);
        repository.AddAuditEvent(CreateAudit(
            "LOGIN_SUCCEEDED",
            "SUCCESS",
            AccountType.Tenant,
            null,
            user.Id,
            client));
        await repository.SaveChangesAsync(cancellationToken);
        var account = Map(user);
        var accessToken = accessTokenService.Create(
            user.Id,
            AccountType.Tenant,
            user.SecurityStamp,
            account.Roles,
            user.OrganizationId,
            user.EmployeeId);
        return new AuthenticationResult(
            accessToken.Value,
            accessToken.ExpiresAt,
            account,
            rawRefreshToken,
            session.ExpiresAt);
    }

    private async Task RequestAccountEmailAsync(
        string email,
        AccountTokenPurpose purpose,
        ClientContext client,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return;
        }

        var normalizedEmail = emailNormalizer.Normalize(email);
        var platform = await repository.FindPlatformByEmailAsync(normalizedEmail, cancellationToken);
        if (platform is not null)
        {
            if (purpose == AccountTokenPurpose.EmailVerification && platform.EmailVerifiedAt is not null)
            {
                return;
            }

            await CreateAndSendAccountTokenAsync(platform, purpose, client, cancellationToken);
            return;
        }

        var tenant = await repository.FindTenantByEmailAsync(normalizedEmail, cancellationToken);
        if (tenant is null ||
            purpose == AccountTokenPurpose.EmailVerification && tenant.EmailVerifiedAt is not null)
        {
            return;
        }

        await CreateAndSendAccountTokenAsync(tenant, purpose, client, cancellationToken);
    }

    private async Task CreateAndSendAccountTokenAsync(
        PlatformUser user,
        AccountTokenPurpose purpose,
        ClientContext client,
        CancellationToken cancellationToken)
    {
        var (rawToken, expiration) = await CreateAccountTokenAsync(
            AccountType.Platform,
            user.Id,
            purpose,
            client,
            cancellationToken);
        await SendAccountEmailSafelyAsync(
            user.Email,
            user.FirstName,
            rawToken,
            expiration,
            purpose,
            AccountType.Platform,
            user.Id,
            null,
            client,
            cancellationToken);
    }

    private async Task CreateAndSendAccountTokenAsync(
        UserAccount user,
        AccountTokenPurpose purpose,
        ClientContext client,
        CancellationToken cancellationToken)
    {
        var (rawToken, expiration) = await CreateAccountTokenAsync(
            AccountType.Tenant,
            user.Id,
            purpose,
            client,
            cancellationToken);
        await SendAccountEmailSafelyAsync(
            user.Email,
            user.FirstName,
            rawToken,
            expiration,
            purpose,
            AccountType.Tenant,
            null,
            user.Id,
            client,
            cancellationToken);
    }

    private async Task<(string RawToken, TimeSpan Expiration)> CreateAccountTokenAsync(
        AccountType accountType,
        Guid accountId,
        AccountTokenPurpose purpose,
        ClientContext client,
        CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var activeTokens = await repository.FindActiveAccountTokensAsync(
            accountType,
            accountId,
            purpose,
            cancellationToken);
        foreach (var existing in activeTokens)
        {
            existing.Revoke(now);
        }

        var expiration = purpose == AccountTokenPurpose.EmailVerification
            ? TimeSpan.FromHours(authenticationOptions.VerificationTokenHours)
            : TimeSpan.FromMinutes(authenticationOptions.PasswordResetTokenMinutes);
        var rawToken = secureTokenService.GenerateToken();
        repository.AddAccountToken(AccountToken.Create(
            accountType,
            accountType == AccountType.Platform ? accountId : null,
            accountType == AccountType.Tenant ? accountId : null,
            purpose,
            secureTokenService.HashToken(rawToken),
            now.Add(expiration),
            now,
            client.IpAddress));
        await repository.SaveChangesAsync(cancellationToken);
        return (rawToken, expiration);
    }

    private async Task SendAccountEmailSafelyAsync(
        string email,
        string firstName,
        string rawToken,
        TimeSpan expiration,
        AccountTokenPurpose purpose,
        AccountType accountType,
        Guid? platformUserId,
        Guid? userAccountId,
        ClientContext client,
        CancellationToken cancellationToken)
    {
        var encodedToken = Uri.EscapeDataString(rawToken);
        var baseUrl = frontendOptions.BaseUrl.TrimEnd('/');
        var isVerification = purpose == AccountTokenPurpose.EmailVerification;
        var url = isVerification
            ? $"{baseUrl}/verify-email?token={encodedToken}"
            : $"{baseUrl}/reset-password?token={encodedToken}";
        var html = isVerification
            ? templateRenderer.RenderVerification(firstName, url, expiration)
            : templateRenderer.RenderPasswordReset(firstName, url, expiration);
        var subject = isVerification
            ? "Verifica tu correo en Legaria"
            : "Restablece tu contraseña de Legaria";

        try
        {
            await emailSender.SendAsync(new EmailMessage(email, subject, html), cancellationToken);
        }
        catch (EmailDeliveryException)
        {
            repository.AddAuditEvent(CreateAudit(
                isVerification ? "EMAIL_VERIFICATION_DELIVERY_FAILED" : "PASSWORD_RESET_DELIVERY_FAILED",
                "FAILED",
                accountType,
                platformUserId,
                userAccountId,
                client));
            await repository.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task<AccountToken> GetUsableAccountTokenAsync(
        string rawToken,
        AccountTokenPurpose purpose,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(rawToken))
        {
            throw InvalidToken();
        }

        var token = await repository.FindAccountTokenAsync(
            secureTokenService.HashToken(rawToken),
            purpose,
            cancellationToken) ?? throw InvalidToken();

        if (token.UsedAt is not null || token.RevokedAt is not null)
        {
            throw new AuthException(AuthErrorCodes.UsedToken, "El enlace ya fue utilizado o reemplazado.");
        }

        if (token.ExpiresAt <= clock.UtcNow)
        {
            throw new AuthException(AuthErrorCodes.ExpiredToken, "El enlace venció.");
        }

        return token;
    }

    private async Task<AuthenticatedAccount> LoadAccountAsync(
        RefreshSession session,
        CancellationToken cancellationToken)
    {
        if (session.PlatformUserId is not null)
        {
            var user = await repository.FindPlatformByIdAsync(session.PlatformUserId.Value, cancellationToken)
                ?? throw new AuthException(AuthErrorCodes.InvalidRefreshToken, "La sesión no es válida.");
            EnsurePlatformAvailable(user, clock.UtcNow);
            return Map(user);
        }

        var tenant = await repository.FindTenantByIdAsync(session.UserAccountId!.Value, cancellationToken)
            ?? throw new AuthException(AuthErrorCodes.InvalidRefreshToken, "La sesión no es válida.");
        await EnsureTenantAvailableAsync(tenant, clock.UtcNow, cancellationToken);
        return Map(tenant);
    }

    private async Task<string> GetSecurityStampAsync(
        AuthenticatedAccount account,
        CancellationToken cancellationToken)
    {
        if (account.AccountType == AccountTypeCodes.Platform)
        {
            var user = await repository.FindPlatformByIdAsync(account.Id, cancellationToken)
                ?? throw new AuthException(AuthErrorCodes.InvalidRefreshToken, "La sesión no es válida.");
            return user.SecurityStamp;
        }

        var tenant = await repository.FindTenantByIdAsync(account.Id, cancellationToken)
            ?? throw new AuthException(AuthErrorCodes.InvalidRefreshToken, "La sesión no es válida.");
        return tenant.SecurityStamp;
    }

    private async Task RevokeFamilyAsync(
        Guid familyId,
        ClientContext client,
        string reason,
        CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var family = await repository.FindSessionsByFamilyAsync(familyId, cancellationToken);
        foreach (var item in family)
        {
            item.Revoke(now, client.IpAddress, reason);
        }
    }

    private static void EnsurePlatformAvailable(PlatformUser user, DateTimeOffset now)
    {
        if (user.Status != AccountStatus.Active)
        {
            throw new AuthException(AuthErrorCodes.AccountUnavailable, "La cuenta está suspendida.");
        }

        if (user.EmailVerifiedAt is null)
        {
            throw new AuthException(AuthErrorCodes.EmailNotVerified, "Debes verificar tu correo antes de ingresar.");
        }

        if (user.IsLockedOut(now))
        {
            throw new AuthException(AuthErrorCodes.AccountLocked, "La cuenta está bloqueada temporalmente.");
        }
    }

    private async Task EnsureTenantAvailableAsync(
        UserAccount user,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (user.Status != AccountStatus.Active ||
            !await repository.IsOrganizationActiveAsync(user.OrganizationId, cancellationToken))
        {
            throw new AuthException(AuthErrorCodes.AccountUnavailable, "La cuenta no está disponible.");
        }

        if (user.EmailVerifiedAt is null)
        {
            throw new AuthException(AuthErrorCodes.EmailNotVerified, "Debes verificar tu correo antes de ingresar.");
        }

        if (user.IsLockedOut(now))
        {
            throw new AuthException(AuthErrorCodes.AccountLocked, "La cuenta está bloqueada temporalmente.");
        }
    }

    private static AuthenticatedAccount Map(PlatformUser user) =>
        new(
            user.Id,
            AccountTypeCodes.Platform,
            user.Email,
            user.FirstName,
            user.LastName,
            [user.Role == PlatformRole.Owner ? PlatformRoleCodes.Owner : PlatformRoleCodes.PlatformAdmin],
            null,
            null);

    private static AuthenticatedAccount Map(UserAccount user) =>
        new(
            user.Id,
            AccountTypeCodes.Tenant,
            user.Email,
            user.FirstName,
            user.LastName,
            user.Roles.Select(item => item.SystemRole.Code).ToArray(),
            user.OrganizationId,
            user.EmployeeId);

    private SecurityAuditEvent CreateAudit(
        string eventType,
        string outcome,
        AccountType accountType,
        Guid? platformUserId,
        Guid? userAccountId,
        ClientContext client) =>
        SecurityAuditEvent.Create(
            eventType,
            outcome,
            clock.UtcNow,
            accountType,
            platformUserId,
            userAccountId,
            client.IpAddress,
            client.UserAgent);

    private static AuthException InvalidCredentials() =>
        new(AuthErrorCodes.InvalidCredentials, "Correo o contraseña incorrectos.");

    private static AuthException InvalidToken() =>
        new(AuthErrorCodes.InvalidToken, "El enlace no es válido.");

    private static void ValidatePassword(string password)
    {
        if (string.IsNullOrEmpty(password) || password.Length is < 8 or > 128)
        {
            throw new AuthException(
                AuthErrorCodes.InvalidPassword,
                "La contraseña debe tener entre 8 y 128 caracteres.");
        }
    }
}
