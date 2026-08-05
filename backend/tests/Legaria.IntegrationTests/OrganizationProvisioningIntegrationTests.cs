using Legaria.Application.Authentication;
using Legaria.Application.Branches;
using Legaria.Application.Configuration;
using Legaria.Application.Organizations;
using Legaria.Domain.Authentication;
using Legaria.Domain.Tenancy;
using Legaria.Infrastructure.Authentication;
using Legaria.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Legaria.IntegrationTests;

[Collection(PostgreSqlCollection.Name)]
public sealed class OrganizationProvisioningIntegrationTests(PostgreSqlFixture fixture)
{
    [Fact]
    public async Task PlatformCanCreateOnlyOneInitialBranchAndOrganizationReportsIt()
    {
        await fixture.ResetAsync();
        await using var context = fixture.CreateDbContext();
        var services = await CreateServicesAsync(context);
        var organization = await services.Organizations.CreateAsync(
            ValidRequest(), services.Actor, Client(), CancellationToken.None);

        Assert.False(organization.HasBranches);
        var branch = await services.Branches.CreateInitialBranchAsync(
            organization.Id,
            InitialBranch(),
            services.Actor,
            Client(),
            CancellationToken.None);

        Assert.Equal(organization.Id, (await context.Branches.SingleAsync()).OrganizationId);
        Assert.Equal("Sede principal", branch.Name);
        Assert.True((await services.Organizations.GetAsync(organization.Id, CancellationToken.None)).HasBranches);
        var audit = await context.SecurityAuditEvents.SingleAsync(item => item.EventType == "BRANCH_CREATED");
        Assert.Equal(services.Actor.UserId, audit.PlatformUserId);
        Assert.Equal(branch.Id, audit.BranchId);

        var duplicate = await Assert.ThrowsAsync<BranchException>(() =>
            services.Branches.CreateInitialBranchAsync(
                organization.Id,
                InitialBranch(),
                services.Actor,
                Client(),
                CancellationToken.None));
        Assert.Equal(BranchErrorCodes.InitialBranchAlreadyExists, duplicate.Code);
    }

    [Fact]
    public async Task InvalidOrMissingOrganizationDoesNotCreateInitialBranch()
    {
        await fixture.ResetAsync();
        await using var context = fixture.CreateDbContext();
        var services = await CreateServicesAsync(context);
        var organization = await services.Organizations.CreateAsync(
            ValidRequest(), services.Actor, Client(), CancellationToken.None);

        var invalid = await Assert.ThrowsAsync<BranchException>(() =>
            services.Branches.CreateInitialBranchAsync(
                organization.Id,
                InitialBranch() with { MunicipalityCode = "99999" },
                services.Actor,
                Client(),
                CancellationToken.None));
        Assert.Equal(BranchErrorCodes.InvalidMunicipality, invalid.Code);
        Assert.Empty(await context.Branches.ToArrayAsync());
        Assert.Single(await context.Organizations.ToArrayAsync());

        var missing = await Assert.ThrowsAsync<OrganizationException>(() =>
            services.Branches.CreateInitialBranchAsync(
                Guid.NewGuid(),
                InitialBranch(),
                services.Actor,
                Client(),
                CancellationToken.None));
        Assert.Equal(OrganizationErrorCodes.NotFound, missing.Code);
    }

    [Fact]
    public async Task ConcurrentInitialBranchRequestsCreateOnlyOneBranch()
    {
        await fixture.ResetAsync();
        Guid organizationId;
        await using (var setupContext = fixture.CreateDbContext())
        {
            var setup = await CreateServicesAsync(setupContext);
            organizationId = (await setup.Organizations.CreateAsync(
                ValidRequest(), setup.Actor, Client(), CancellationToken.None)).Id;
        }

        await using var firstContext = fixture.CreateDbContext();
        await using var secondContext = fixture.CreateDbContext();
        var first = await CreateServicesAsync(firstContext);
        var second = await CreateServicesAsync(secondContext);
        var results = await Task.WhenAll(
            CaptureBranchAsync(() => first.Branches.CreateInitialBranchAsync(
                organizationId, InitialBranch(), first.Actor, Client(), CancellationToken.None)),
            CaptureBranchAsync(() => second.Branches.CreateInitialBranchAsync(
                organizationId, InitialBranch() with { Name = "Alterna" }, second.Actor, Client(), CancellationToken.None)));

        Assert.Single(results, item => item is null);
        Assert.Single(results, item => item is BranchException { Code: BranchErrorCodes.InitialBranchAlreadyExists });
        await using var verificationContext = fixture.CreateDbContext();
        Assert.Single(await verificationContext.Branches.ToArrayAsync());
    }

    [Fact]
    public async Task MigrationLoadsVersionedDivipolaAndBackfillsGlobalOwnerEmail()
    {
        await fixture.ResetAsync();
        await using var context = fixture.CreateDbContext();
        var services = await CreateServicesAsync(context);

        Assert.Equal(33, await context.Departments.CountAsync());
        Assert.Equal(1122, await context.Municipalities.CountAsync());
        Assert.Equal("BOGOTÁ, D.C.", (await context.Municipalities.SingleAsync(item => item.Code == "11001")).Name);
        Assert.True(await context.AccountEmails.AnyAsync(item => item.PlatformUserId == services.Actor.UserId));
    }

    [Fact]
    public async Task CreationAndAcceptanceAreCompleteHashedAuditedAndSingleUse()
    {
        await fixture.ResetAsync();
        await using var context = fixture.CreateDbContext();
        var services = await CreateServicesAsync(context);

        var created = await services.Organizations.CreateAsync(
            ValidRequest(),
            services.Actor,
            Client(),
            CancellationToken.None);

        Assert.Equal(InvitationStatuses.Sent, created.InitialAdmin.InvitationStatus);
        Assert.Equal(OrganizationStatus.Active, (await context.Organizations.SingleAsync()).Status);
        var account = await context.UserAccounts
            .Include(item => item.Roles)
            .SingleAsync();
        Assert.True(account.IsInitialAdministrator);
        Assert.Null(account.EmailVerifiedAt);
        Assert.Null(account.EmployeeId);
        Assert.Single(account.Roles, role => role.SystemRoleId == SystemRole.SuperAdminId);
        Assert.Equal(1, await context.AccountEmails.CountAsync(item => item.UserAccountId == account.Id));
        Assert.DoesNotContain("Invitada.2026!", account.PasswordHash, StringComparison.Ordinal);

        var rawToken = ExtractToken(services.Email.LastHtml);
        var stored = await context.AccountTokens.SingleAsync();
        Assert.NotEqual(rawToken, stored.TokenHash);
        Assert.NotNull(stored.DeliveredAt);

        await services.Organizations.AcceptInvitationAsync(
            new AcceptInvitationRequest(rawToken, "Invitada.2026!"),
            Client(),
            CancellationToken.None);

        account = await context.UserAccounts.SingleAsync();
        Assert.NotNull(account.EmailVerifiedAt);
        Assert.True(services.Passwords.Verify(account.PasswordHash, "Invitada.2026!"));
        Assert.NotNull((await context.AccountTokens.SingleAsync()).UsedAt);
        Assert.Contains(
            await context.SecurityAuditEvents.Select(item => item.EventType).ToArrayAsync(),
            type => type == "ORGANIZATION_CREATED");
        Assert.Contains(
            await context.SecurityAuditEvents.Select(item => item.EventType).ToArrayAsync(),
            type => type == "TENANT_INVITATION_ACCEPTED");

        var reused = await Assert.ThrowsAsync<OrganizationException>(() =>
            services.Organizations.AcceptInvitationAsync(
                new AcceptInvitationRequest(rawToken, "OtraClave.2026!"),
                Client(),
                CancellationToken.None));
        Assert.Equal(OrganizationErrorCodes.UsedInvitation, reused.Code);
    }

    [Fact]
    public async Task FailedDeliveryKeepsOrganizationAndResendRevokesPreviousInvitation()
    {
        await fixture.ResetAsync();
        await using var context = fixture.CreateDbContext();
        var services = await CreateServicesAsync(context);
        services.Email.ShouldFail = true;

        var created = await services.Organizations.CreateAsync(
            ValidRequest(),
            services.Actor,
            Client(),
            CancellationToken.None);
        Assert.Equal(InvitationStatuses.DeliveryFailed, created.InitialAdmin.InvitationStatus);
        Assert.Single(await context.Organizations.ToArrayAsync());
        var previous = await context.AccountTokens.SingleAsync();

        services.Email.ShouldFail = false;
        var resent = await services.Organizations.ResendInvitationAsync(
            created.Id,
            services.Actor,
            Client(),
            CancellationToken.None);

        Assert.Equal(InvitationStatuses.Sent, resent.InitialAdmin.InvitationStatus);
        var tokens = await context.AccountTokens.OrderBy(item => item.CreatedAt).ToArrayAsync();
        Assert.Equal(2, tokens.Length);
        Assert.NotNull(tokens.Single(item => item.Id == previous.Id).RevokedAt);
        Assert.NotNull(tokens.Single(item => item.Id != previous.Id).DeliveredAt);
    }

    [Fact]
    public async Task SuspensionBlocksLoginAndRefreshAndReactivationPreservesSession()
    {
        await fixture.ResetAsync();
        await using var context = fixture.CreateDbContext();
        var services = await CreateServicesAsync(context);
        var created = await services.Organizations.CreateAsync(
            ValidRequest(),
            services.Actor,
            Client(),
            CancellationToken.None);
        await services.Organizations.AcceptInvitationAsync(
            new AcceptInvitationRequest(ExtractToken(services.Email.LastHtml), "Invitada.2026!"),
            Client(),
            CancellationToken.None);
        var login = await services.Authentication.LoginAsync(
            new LoginRequest("admin@empresa.test", "Invitada.2026!"),
            Client(),
            CancellationToken.None);

        await services.Organizations.SuspendAsync(
            created.Id,
            services.Actor,
            Client(),
            CancellationToken.None);
        var rejectedLogin = await Assert.ThrowsAsync<AuthException>(() =>
            services.Authentication.LoginAsync(
                new LoginRequest("admin@empresa.test", "Invitada.2026!"),
                Client(),
                CancellationToken.None));
        Assert.Equal(AuthErrorCodes.AccountUnavailable, rejectedLogin.Code);
        var rejectedRefresh = await Assert.ThrowsAsync<AuthException>(() =>
            services.Authentication.RefreshAsync(login.RefreshToken, Client(), CancellationToken.None));
        Assert.Equal(AuthErrorCodes.AccountUnavailable, rejectedRefresh.Code);

        await services.Organizations.ReactivateAsync(
            created.Id,
            services.Actor,
            Client(),
            CancellationToken.None);
        var refreshed = await services.Authentication.RefreshAsync(
            login.RefreshToken,
            Client(),
            CancellationToken.None);
        Assert.Equal(created.Id, refreshed.Account.OrganizationId);
    }

    [Fact]
    public async Task GlobalEmailRegistryAllowsOnlyOneConcurrentTenantProvisioning()
    {
        await fixture.ResetAsync();
        Guid actorId;
        await using (var bootstrapContext = fixture.CreateDbContext())
        {
            actorId = (await CreateServicesAsync(bootstrapContext)).Actor.UserId;
        }

        await using var firstContext = fixture.CreateDbContext();
        await using var secondContext = fixture.CreateDbContext();
        var firstServices = await CreateServicesAsync(firstContext);
        var secondServices = await CreateServicesAsync(secondContext);
        Assert.Equal(actorId, firstServices.Actor.UserId);
        Assert.Equal(actorId, secondServices.Actor.UserId);

        var firstRequest = ValidRequest();
        var secondRequest = firstRequest with
        {
            TradeName = "Empresa Concurrente",
            LegalName = "Empresa Concurrente S.A.S.",
            Nit = "800197268",
            VerificationDigit = 4
        };
        var outcomes = await Task.WhenAll(
            CaptureAsync(() => firstServices.Organizations.CreateAsync(
                firstRequest,
                firstServices.Actor,
                Client(),
                CancellationToken.None)),
            CaptureAsync(() => secondServices.Organizations.CreateAsync(
                secondRequest,
                secondServices.Actor,
                Client(),
                CancellationToken.None)));

        Assert.Single(outcomes, outcome => outcome is null);
        var conflict = Assert.IsType<OrganizationException>(Assert.Single(outcomes, outcome => outcome is not null));
        Assert.Equal(OrganizationErrorCodes.DuplicateAccountEmail, conflict.Code);

        await using var verificationContext = fixture.CreateDbContext();
        Assert.Equal(1, await verificationContext.Organizations.CountAsync());
        Assert.Equal(1, await verificationContext.UserAccounts.CountAsync());
        Assert.Equal(2, await verificationContext.AccountEmails.CountAsync());
    }

    private static async Task<TestServices> CreateServicesAsync(LegariaDbContext context)
    {
        var authenticationRepository = new AuthenticationRepository(context);
        var organizationRepository = new OrganizationRepository(context);
        var passwords = new PasswordService();
        var normalizer = new EmailNormalizer();
        var tokens = new SecureTokenService();
        var clock = new FixedClock(new DateTimeOffset(2026, 8, 4, 18, 0, 0, TimeSpan.Zero));
        var email = new TestEmailSender();
        var renderer = new TestRenderer();
        var authenticationOptions = new Legaria.Application.Configuration.AuthenticationOptions();
        var frontendOptions = new FrontendOptions { BaseUrl = "https://legaria.test" };
        var bootstrapper = new PlatformOwnerBootstrapper(
            authenticationRepository,
            normalizer,
            passwords,
            tokens,
            clock,
            new BootstrapOwnerOptions
            {
                Email = "owner@legaria.test",
                Password = "bootstrap-123",
                FirstName = "Owner",
                LastName = "Legaria"
            });
        await bootstrapper.BootstrapAsync(CancellationToken.None);
        var owner = await context.PlatformUsers.SingleAsync();
        var actor = new CurrentAccount(
            owner.Id,
            AccountType.Platform,
            null,
            null,
            [PlatformRoleCodes.Owner]);
        var invitations = new TenantInvitationService(
            new TenantInvitationRepository(context),
            passwords,
            tokens,
            email,
            renderer,
            clock,
            authenticationOptions,
            frontendOptions);
        var organizations = new OrganizationService(
            organizationRepository,
            new NitValidator(),
            normalizer,
            passwords,
            tokens,
            clock,
            invitations);
        var branches = new BranchService(
            new BranchRepository(context),
            normalizer,
            tokens,
            invitations,
            clock);
        var authentication = new AuthenticationService(
            authenticationRepository,
            passwords,
            normalizer,
            tokens,
            new JwtAccessTokenService(
                new JwtOptions
                {
                    Issuer = "legaria-tests",
                    Audience = "legaria-tests",
                    SigningKey = "tests-only-signing-key-with-at-least-32-bytes",
                    AccessTokenMinutes = 10
                },
                clock),
            email,
            renderer,
            clock,
            authenticationOptions,
            frontendOptions);
        return new TestServices(organizations, branches, authentication, passwords, email, actor);
    }

    private static CreateOrganizationRequest ValidRequest() => new(
        "Empresa Demo",
        "Empresa Demo S.A.S.",
        "900373913",
        4,
        "contacto@empresa.test",
        "+573001112233",
        "Carrera 7 # 10-20",
        "11001",
        new InitialAdministratorInput("Ana", "Prueba", "admin@empresa.test"));

    private static ClientContext Client() => new("127.0.0.1", "organization-tests");

    private static BranchInput InitialBranch() => new(
        "Sede principal",
        "contacto@empresa.test",
        "+573001112233",
        "Carrera 7 # 10-20",
        "11001");

    private static async Task<Exception?> CaptureBranchAsync(Func<Task<BranchResult>> action)
    {
        try
        {
            await action();
            return null;
        }
        catch (Exception exception)
        {
            return exception;
        }
    }

    private static async Task<Exception?> CaptureAsync(Func<Task<OrganizationResult>> action)
    {
        try
        {
            await action();
            return null;
        }
        catch (Exception exception)
        {
            return exception;
        }
    }

    private static string ExtractToken(string html)
    {
        var uri = new Uri(html);
        return Uri.UnescapeDataString(uri.Query["?token=".Length..]);
    }

    private sealed record TestServices(
        OrganizationService Organizations,
        BranchService Branches,
        AuthenticationService Authentication,
        PasswordService Passwords,
        TestEmailSender Email,
        CurrentAccount Actor);

    private sealed class FixedClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow { get; } = now;
    }

    private sealed class TestEmailSender : IEmailSender
    {
        public bool ShouldFail { get; set; }
        public string LastHtml { get; private set; } = string.Empty;

        public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
        {
            if (ShouldFail)
            {
                throw new EmailDeliveryException("Fallo controlado de pruebas.");
            }

            LastHtml = message.HtmlBody;
            return Task.CompletedTask;
        }
    }

    private sealed class TestRenderer : IEmailTemplateRenderer
    {
        public string RenderVerification(string firstName, string verificationUrl, TimeSpan expiration) => verificationUrl;
        public string RenderPasswordReset(string firstName, string resetUrl, TimeSpan expiration) => resetUrl;
        public string RenderTenantInvitation(
            string firstName,
            string organizationName,
            string accessProfile,
            string invitationUrl,
            TimeSpan expiration) => invitationUrl;
    }
}
