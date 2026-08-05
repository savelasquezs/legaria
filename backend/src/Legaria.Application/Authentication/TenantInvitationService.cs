using Legaria.Application.Configuration;
using Legaria.Application.Organizations;
using Legaria.Domain.Authentication;
using Legaria.Domain.Tenancy;

namespace Legaria.Application.Authentication;

public sealed record IssuedTenantInvitation(AccountToken Token, string RawToken);

public interface ITenantInvitationRepository
{
    Task<AccountToken?> FindByHashAsync(string tokenHash, CancellationToken cancellationToken);
    Task<UserAccount?> FindAccountAsync(Guid accountId, CancellationToken cancellationToken);
    Task<Organization?> FindOrganizationAsync(Guid organizationId, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<AccountToken>> FindActiveAsync(Guid accountId, CancellationToken cancellationToken);
    void AddToken(AccountToken token);
    void AddAuditEvent(SecurityAuditEvent auditEvent);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public interface ITenantInvitationService
{
    Task<IssuedTenantInvitation> IssueAsync(
        Guid accountId,
        ClientContext client,
        bool revokeExisting,
        CancellationToken cancellationToken);
    Task DeliverAsync(
        Organization organization,
        UserAccount account,
        IssuedTenantInvitation invitation,
        string accessProfile,
        CurrentAccount actor,
        ClientContext client,
        CancellationToken cancellationToken);
    Task AcceptAsync(AcceptInvitationRequest request, ClientContext client, CancellationToken cancellationToken);
    string GetPublicStatus(UserAccount account, AccountToken? token, DateTimeOffset now);
}

public sealed class TenantInvitationService(
    ITenantInvitationRepository repository,
    IPasswordService passwordService,
    ISecureTokenService secureTokenService,
    IEmailSender emailSender,
    IEmailTemplateRenderer templateRenderer,
    IClock clock,
    AuthenticationOptions authenticationOptions,
    FrontendOptions frontendOptions) : ITenantInvitationService
{
    private TimeSpan Lifetime => TimeSpan.FromHours(authenticationOptions.VerificationTokenHours);

    public async Task<IssuedTenantInvitation> IssueAsync(
        Guid accountId,
        ClientContext client,
        bool revokeExisting,
        CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        if (revokeExisting)
        {
            foreach (var current in await repository.FindActiveAsync(accountId, cancellationToken))
            {
                current.Revoke(now);
            }
        }

        var rawToken = secureTokenService.GenerateToken();
        var token = AccountToken.Create(
            AccountType.Tenant,
            null,
            accountId,
            AccountTokenPurpose.TenantInvitation,
            secureTokenService.HashToken(rawToken),
            now.Add(Lifetime),
            now,
            client.IpAddress);
        repository.AddToken(token);
        return new IssuedTenantInvitation(token, rawToken);
    }

    public async Task DeliverAsync(
        Organization organization,
        UserAccount account,
        IssuedTenantInvitation invitation,
        string accessProfile,
        CurrentAccount actor,
        ClientContext client,
        CancellationToken cancellationToken)
    {
        var url = $"{frontendOptions.BaseUrl.TrimEnd('/')}/accept-invitation?token={Uri.EscapeDataString(invitation.RawToken)}";
        var now = clock.UtcNow;
        try
        {
            await emailSender.SendAsync(
                new EmailMessage(
                    account.Email,
                    $"Activa tu cuenta de {organization.TradeName} en Legaria",
                    templateRenderer.RenderTenantInvitation(
                        account.FirstName,
                        organization.TradeName,
                        accessProfile,
                        url,
                        Lifetime)),
                cancellationToken);
            invitation.Token.MarkDelivered(now);
            repository.AddAuditEvent(CreateAudit(
                "TENANT_INVITATION_DELIVERED",
                actor,
                organization.Id,
                account.Id,
                client,
                now));
        }
        catch (EmailDeliveryException)
        {
            invitation.Token.MarkDeliveryFailed(now);
            repository.AddAuditEvent(CreateAudit(
                "TENANT_INVITATION_DELIVERY_FAILED",
                actor,
                organization.Id,
                account.Id,
                client,
                now,
                "FAILED"));
        }

        await repository.SaveChangesAsync(cancellationToken);
    }

    public async Task AcceptAsync(
        AcceptInvitationRequest request,
        ClientContext client,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Token))
        {
            throw InvalidInvitation();
        }

        if (request.NewPassword is null || request.NewPassword.Length is < 8 or > 128)
        {
            throw new OrganizationException(
                AuthErrorCodes.InvalidPassword,
                "La contraseÃ±a debe tener entre 8 y 128 caracteres.");
        }

        var now = clock.UtcNow;
        var invitation = await repository.FindByHashAsync(
            secureTokenService.HashToken(request.Token),
            cancellationToken) ?? throw InvalidInvitation();
        if (invitation.UsedAt is not null || invitation.RevokedAt is not null)
        {
            throw new OrganizationException(
                OrganizationErrorCodes.UsedInvitation,
                "La invitaciÃ³n ya fue utilizada o reemplazada.",
                OrganizationErrorKind.Conflict);
        }

        if (invitation.ExpiresAt <= now)
        {
            throw new OrganizationException(
                OrganizationErrorCodes.ExpiredInvitation,
                "La invitaciÃ³n expirÃ³.",
                OrganizationErrorKind.Conflict);
        }

        var account = invitation.UserAccountId is { } userId
            ? await repository.FindAccountAsync(userId, cancellationToken)
            : null;
        if (account is null || account.EmailVerifiedAt is not null)
        {
            throw new OrganizationException(
                OrganizationErrorCodes.UsedInvitation,
                "La invitaciÃ³n ya fue utilizada.",
                OrganizationErrorKind.Conflict);
        }

        if (account.Status != AccountStatus.Active)
        {
            throw new OrganizationException(
                AuthErrorCodes.AccountUnavailable,
                "La cuenta estÃ¡ suspendida.",
                OrganizationErrorKind.Forbidden);
        }

        var organization = await repository.FindOrganizationAsync(account.OrganizationId, cancellationToken)
            ?? throw InvalidInvitation();
        if (organization.Status != OrganizationStatus.Active)
        {
            throw new OrganizationException(
                OrganizationErrorCodes.SuspendedOrganization,
                "La organizaciÃ³n estÃ¡ suspendida.",
                OrganizationErrorKind.Forbidden);
        }

        account.ChangePassword(
            passwordService.Hash(request.NewPassword),
            secureTokenService.GenerateSecurityStamp(),
            now);
        account.VerifyEmail(now);
        invitation.MarkUsed(now);
        repository.AddAuditEvent(SecurityAuditEvent.Create(
            "TENANT_INVITATION_ACCEPTED",
            "SUCCESS",
            now,
            AccountType.Tenant,
            userAccountId: account.Id,
            ipAddress: client.IpAddress,
            userAgent: client.UserAgent,
            organizationId: organization.Id,
            actorUserAccountId: account.Id));
        await repository.SaveChangesAsync(cancellationToken);
    }

    public string GetPublicStatus(UserAccount account, AccountToken? token, DateTimeOffset now)
    {
        if (account.EmailVerifiedAt is not null || token?.UsedAt is not null)
        {
            return InvitationStatuses.Accepted;
        }

        if (token?.RevokedAt is not null)
        {
            return InvitationStatuses.Revoked;
        }

        if (token is null)
        {
            return InvitationStatuses.PendingDelivery;
        }

        if (token.ExpiresAt <= now)
        {
            return InvitationStatuses.Expired;
        }

        if (token.DeliveryFailedAt is not null)
        {
            return InvitationStatuses.DeliveryFailed;
        }

        return token.DeliveredAt is not null
            ? InvitationStatuses.Sent
            : InvitationStatuses.PendingDelivery;
    }

    private static OrganizationException InvalidInvitation() => new(
        OrganizationErrorCodes.InvalidInvitation,
        "La invitaciÃ³n no es vÃ¡lida.");

    private static SecurityAuditEvent CreateAudit(
        string eventType,
        CurrentAccount actor,
        Guid organizationId,
        Guid affectedAccountId,
        ClientContext client,
        DateTimeOffset now,
        string outcome = "SUCCESS") =>
        SecurityAuditEvent.Create(
            eventType,
            outcome,
            now,
            actor.AccountType,
            platformUserId: actor.AccountType == AccountType.Platform ? actor.UserId : null,
            userAccountId: affectedAccountId,
            ipAddress: client.IpAddress,
            userAgent: client.UserAgent,
            organizationId: organizationId,
            actorUserAccountId: actor.AccountType == AccountType.Tenant ? actor.UserId : null);
}
