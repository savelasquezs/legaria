using Legaria.Application.Authentication;
using Legaria.Application.Branches;
using Legaria.Application.Configuration;
using Legaria.Application.Employees;
using Legaria.Application.Organizations;
using Legaria.Domain.Authentication;
using Legaria.Domain.Employees;
using Legaria.Domain.Tenancy;
using Legaria.Infrastructure.Authentication;
using Legaria.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Legaria.IntegrationTests;

[Collection(PostgreSqlCollection.Name)]
public sealed class BranchAdministrationIntegrationTests(PostgreSqlFixture fixture)
{
    [Fact]
    public async Task BranchesAreUniquePerTenantAndBranchAdministratorOnlySeesAssignedBranches()
    {
        await fixture.ResetAsync();
        await using var context = fixture.CreateDbContext();
        var services = await CreateServicesAsync(context);
        var assigned = await services.Branches.CreateBranchAsync(
            BranchInput("Centro"), services.SuperActor, Client(), CancellationToken.None);
        var hidden = await services.Branches.CreateBranchAsync(
            BranchInput("Norte"), services.SuperActor, Client(), CancellationToken.None);
        var otherTenant = await services.Branches.CreateBranchAsync(
            BranchInput("Centro"), services.OtherSuperActor, Client(), CancellationToken.None);
        var administrator = await CreateAdministratorAsync(services, assigned.Id, [assigned.Id]);
        var administratorId = administrator.AdministrativeAccess!.AccountId;
        var branchActor = new CurrentAccount(
            administratorId,
            AccountType.Tenant,
            services.SuperActor.OrganizationId,
            administrator.Id,
            [SystemRoleCodes.BranchAdmin]);

        var visible = await services.Branches.ListBranchesAsync(
            1, 20, null, null, branchActor, CancellationToken.None);

        Assert.Equal(assigned.Id, Assert.Single(visible.Items).Id);
        Assert.Equal(
            BranchErrorCodes.NotFound,
            (await Assert.ThrowsAsync<BranchException>(() => services.Branches.GetBranchAsync(
                hidden.Id, branchActor, CancellationToken.None))).Code);
        Assert.Equal(
            BranchErrorCodes.NotFound,
            (await Assert.ThrowsAsync<BranchException>(() => services.Branches.GetBranchAsync(
                otherTenant.Id, branchActor, CancellationToken.None))).Code);
        Assert.Equal(
            BranchErrorCodes.Forbidden,
            (await Assert.ThrowsAsync<BranchException>(() => services.Branches.CreateBranchAsync(
                BranchInput("Prohibida"), branchActor, Client(), CancellationToken.None))).Code);

        var duplicate = await Assert.ThrowsAsync<BranchException>(() =>
            services.Branches.CreateBranchAsync(
                BranchInput("  centro  "), services.SuperActor, Client(), CancellationToken.None));
        Assert.Equal(BranchErrorCodes.DuplicateName, duplicate.Code);
    }

    [Fact]
    public async Task InvitationAndAccessChangesAreTransactionalHashedHistoricalAndImmediate()
    {
        await fixture.ResetAsync();
        await using var context = fixture.CreateDbContext();
        var services = await CreateServicesAsync(context);
        var first = await services.Branches.CreateBranchAsync(
            BranchInput("Centro"), services.SuperActor, Client(), CancellationToken.None);
        var second = await services.Branches.CreateBranchAsync(
            BranchInput("Norte"), services.SuperActor, Client(), CancellationToken.None);

        var created = await CreateAdministratorAsync(services, first.Id, [first.Id]);
        var accountId = created.AdministrativeAccess!.AccountId;
        var account = await context.UserAccounts
            .Include(item => item.Roles)
            .SingleAsync(item => item.Id == accountId);
        var firstRawToken = ExtractToken(services.Email.LastHtml);
        var firstStoredToken = await context.AccountTokens.SingleAsync(item => item.UserAccountId == accountId);

        Assert.Equal(created.Id, account.EmployeeId);
        Assert.Single(account.Roles, role => role.SystemRoleId == SystemRole.BranchAdminId);
        Assert.NotEqual(firstRawToken, firstStoredToken.TokenHash);
        Assert.Equal(first.Id, (await context.UserBranchAccesses.SingleAsync()).BranchId);
        Assert.Equal(InvitationStatuses.Sent, created.AdministrativeAccess.InvitationStatus);

        await services.Branches.ResendInvitationAsync(
            accountId, services.SuperActor, Client(), CancellationToken.None);
        var secondRawToken = ExtractToken(services.Email.LastHtml);
        Assert.NotNull((await context.AccountTokens.SingleAsync(item => item.Id == firstStoredToken.Id)).RevokedAt);
        var replaced = await Assert.ThrowsAsync<OrganizationException>(() => services.Invitations.AcceptAsync(
            new AcceptInvitationRequest(firstRawToken, "Admin.2026!"), Client(), CancellationToken.None));
        Assert.Equal(OrganizationErrorCodes.UsedInvitation, replaced.Code);

        await services.Invitations.AcceptAsync(
            new AcceptInvitationRequest(secondRawToken, "Admin.2026!"), Client(), CancellationToken.None);
        Assert.NotNull((await context.UserAccounts.SingleAsync(item => item.Id == accountId)).EmailVerifiedAt);

        await services.Branches.UpdateAssignmentsAsync(
            accountId,
            new UpdateBranchAssignmentsRequest([second.Id]),
            services.SuperActor,
            Client(),
            CancellationToken.None);
        var history = await context.UserBranchAccesses
            .Where(item => item.UserAccountId == accountId)
            .OrderBy(item => item.GrantedAt)
            .ToArrayAsync();
        Assert.Equal(2, history.Length);
        Assert.NotNull(history.Single(item => item.BranchId == first.Id).RevokedAt);
        Assert.Null(history.Single(item => item.BranchId == second.Id).RevokedAt);

        var branchActor = new CurrentAccount(
            accountId,
            AccountType.Tenant,
            services.SuperActor.OrganizationId,
            created.Id,
            [SystemRoleCodes.BranchAdmin]);
        var visible = await services.Branches.ListBranchesAsync(
            1, 20, null, null, branchActor, CancellationToken.None);
        Assert.Equal(second.Id, Assert.Single(visible.Items).Id);
    }

    [Fact]
    public async Task SuspendingAdministratorRevokesSessionsStampAndPendingInvitations()
    {
        await fixture.ResetAsync();
        await using var context = fixture.CreateDbContext();
        var services = await CreateServicesAsync(context);
        var branch = await services.Branches.CreateBranchAsync(
            BranchInput("Centro"), services.SuperActor, Client(), CancellationToken.None);
        var accepted = await CreateAdministratorAsync(services, branch.Id, [branch.Id]);
        var acceptedAccountId = accepted.AdministrativeAccess!.AccountId;
        await services.Invitations.AcceptAsync(
            new AcceptInvitationRequest(ExtractToken(services.Email.LastHtml), "Admin.2026!"),
            Client(),
            CancellationToken.None);
        var login = await services.Authentication.LoginAsync(
            new LoginRequest("branch.admin@legaria.test", "Admin.2026!"),
            Client(),
            CancellationToken.None);
        var previousStamp = (await context.UserAccounts.SingleAsync(item => item.Id == acceptedAccountId)).SecurityStamp;

        await services.Branches.SuspendAdministratorAsync(
            acceptedAccountId, services.SuperActor, Client(), CancellationToken.None);
        var suspended = await context.UserAccounts.SingleAsync(item => item.Id == acceptedAccountId);
        Assert.Equal(AccountStatus.Suspended, suspended.Status);
        Assert.NotEqual(previousStamp, suspended.SecurityStamp);
        Assert.All(
            await context.RefreshSessions.Where(item => item.UserAccountId == acceptedAccountId).ToArrayAsync(),
            session => Assert.NotNull(session.RevokedAt));
        var rejectedRefresh = await Assert.ThrowsAsync<AuthException>(() => services.Authentication.RefreshAsync(
            login.RefreshToken, Client(), CancellationToken.None));
        Assert.Equal(AuthErrorCodes.InvalidRefreshToken, rejectedRefresh.Code);

        await services.Branches.ReactivateAdministratorAsync(
            acceptedAccountId, services.SuperActor, Client(), CancellationToken.None);
        var newLogin = await services.Authentication.LoginAsync(
            new LoginRequest("branch.admin@legaria.test", "Admin.2026!"),
            Client(),
            CancellationToken.None);
        Assert.Equal(acceptedAccountId, newLogin.Account.Id);

        var pending = await CreateAdministratorAsync(
            services,
            branch.Id,
            [branch.Id],
            "pending.admin@legaria.test");
        var pendingAccountId = pending.AdministrativeAccess!.AccountId;
        var pendingToken = ExtractToken(services.Email.LastHtml);
        await services.Branches.SuspendAdministratorAsync(
            pendingAccountId, services.SuperActor, Client(), CancellationToken.None);
        var rejectedInvitation = await Assert.ThrowsAsync<OrganizationException>(() => services.Invitations.AcceptAsync(
            new AcceptInvitationRequest(pendingToken, "Pending.2026!"), Client(), CancellationToken.None));
        Assert.Equal(OrganizationErrorCodes.UsedInvitation, rejectedInvitation.Code);
        await services.Branches.ReactivateAdministratorAsync(
            pendingAccountId, services.SuperActor, Client(), CancellationToken.None);
        Assert.Equal(
            InvitationStatuses.Revoked,
            (await services.Branches.GetAdministratorAsync(
                pendingAccountId, services.SuperActor, CancellationToken.None)).InvitationStatus);
    }

    [Fact]
    public async Task DeactivatingBranchPreservesAccessAndRejectsItForNewAssignments()
    {
        await fixture.ResetAsync();
        await using var context = fixture.CreateDbContext();
        var services = await CreateServicesAsync(context);
        var branch = await services.Branches.CreateBranchAsync(
            BranchInput("Centro"), services.SuperActor, Client(), CancellationToken.None);
        var administrator = await CreateAdministratorAsync(services, branch.Id, [branch.Id]);
        var administratorId = administrator.AdministrativeAccess!.AccountId;

        await services.Branches.DeactivateBranchAsync(
            branch.Id, services.SuperActor, Client(), CancellationToken.None);

        Assert.Null((await context.UserBranchAccesses.SingleAsync()).RevokedAt);
        var branchActor = new CurrentAccount(
            administratorId,
            AccountType.Tenant,
            services.SuperActor.OrganizationId,
            administrator.Id,
            [SystemRoleCodes.BranchAdmin]);
        var visible = await services.Branches.ListBranchesAsync(
            1, 20, null, null, branchActor, CancellationToken.None);
        Assert.Equal(BranchStatuses.Inactive, Assert.Single(visible.Items).Status);
        var invalid = await Assert.ThrowsAsync<BranchException>(() => services.Branches.UpdateAssignmentsAsync(
            administratorId,
            new UpdateBranchAssignmentsRequest([branch.Id]),
            services.SuperActor,
            Client(),
            CancellationToken.None));
        Assert.Equal(BranchErrorCodes.InvalidBranchAccess, invalid.Code);

        await services.Branches.ReactivateBranchAsync(
            branch.Id, services.SuperActor, Client(), CancellationToken.None);
        Assert.Equal(
            BranchStatuses.Active,
            (await services.Branches.ListBranchesAsync(
                1, 20, null, null, branchActor, CancellationToken.None)).Items.Single().Status);
    }

    [Fact]
    public async Task WorkerCanExistWithoutAccountAndBeAssignedToMultipleBranches()
    {
        await fixture.ResetAsync();
        await using var context = fixture.CreateDbContext();
        var services = await CreateServicesAsync(context);
        var first = await services.Branches.CreateBranchAsync(
            BranchInput("Centro"), services.SuperActor, Client(), CancellationToken.None);
        var second = await services.Branches.CreateBranchAsync(
            BranchInput("Norte"), services.SuperActor, Client(), CancellationToken.None);

        var created = await services.Employees.CreateAsync(
            first.Id,
            new CreateEmployeeInput(
                "CC",
                "1030123456",
                "María",
                "Trabajadora",
                services.PositionId,
                new DateOnly(2026, 8, 4),
                true,
                null),
            services.SuperActor,
            Client(),
            CancellationToken.None);
        var assigned = await services.Employees.AssignAsync(
            second.Id,
            created.Id,
            new AssignEmployeeInput(
                services.PositionId,
                new DateOnly(2026, 8, 5),
                false,
                null),
            services.SuperActor,
            Client(),
            CancellationToken.None);

        Assert.Null(assigned.AdministrativeAccess);
        Assert.Equal(2, assigned.Assignments.Count);
        Assert.Empty(await context.UserAccounts.Where(item => item.EmployeeId == created.Id).ToArrayAsync());
        Assert.Equal(
            created.Id,
            Assert.Single((await services.Employees.ListAsync(
                1, 20, null, second.Id, null, services.SuperActor, CancellationToken.None)).Items).Id);

        var invited = await services.Employees.GrantAdministrativeAccessAsync(
            created.Id,
            new AdministrativeAccessInput("worker.admin@legaria.test", [first.Id, second.Id]),
            services.SuperActor,
            Client(),
            CancellationToken.None);

        var access = Assert.IsType<EmployeeAdministrativeAccessResult>(invited.AdministrativeAccess);
        Assert.Equal(created.Id, (await context.UserAccounts.SingleAsync(item => item.Id == access.AccountId)).EmployeeId);
        Assert.Equal(2, access.BranchIds.Count);
    }

    private static async Task<TestServices> CreateServicesAsync(LegariaDbContext context)
    {
        var now = new DateTimeOffset(2026, 8, 4, 20, 0, 0, TimeSpan.Zero);
        var clock = new FixedClock(now);
        var passwords = new PasswordService();
        var normalizer = new EmailNormalizer();
        var tokens = new SecureTokenService();
        var email = new CapturingEmailSender();
        var renderer = new PassThroughRenderer();
        var authenticationOptions = new Legaria.Application.Configuration.AuthenticationOptions();
        var frontendOptions = new FrontendOptions { BaseUrl = "https://legaria.test" };
        var organizationA = Organization.Create(
            "Organización A", "Organización A S.A.S.", "900373913", 4,
            "contacto-a@legaria.test", "+573001112233", "Calle 1", "11001", now);
        var organizationB = Organization.Create(
            "Organización B", "Organización B S.A.S.", "800197268", 4,
            "contacto-b@legaria.test", "+573001112244", "Calle 2", "11001", now);
        var superA = UserAccount.Create(
            organizationA.Id, null, "super.a@legaria.test", normalizer.Normalize("super.a@legaria.test"),
            passwords.Hash("Super.2026!"), "Super", "A", tokens.GenerateSecurityStamp(), true, now, true);
        var superB = UserAccount.Create(
            organizationB.Id, null, "super.b@legaria.test", normalizer.Normalize("super.b@legaria.test"),
            passwords.Hash("Super.2026!"), "Super", "B", tokens.GenerateSecurityStamp(), true, now, true);
        superA.AddRole(SystemRole.SuperAdminId);
        superB.AddRole(SystemRole.SuperAdminId);
        context.AddRange(
            organizationA,
            organizationB,
            superA,
            superB,
            AccountEmail.ForTenant(superA.NormalizedEmail, superA.Id, now),
            AccountEmail.ForTenant(superB.NormalizedEmail, superB.Id, now));
        await context.SaveChangesAsync();

        var invitations = new TenantInvitationService(
            new TenantInvitationRepository(context),
            passwords,
            tokens,
            email,
            renderer,
            clock,
            authenticationOptions,
            frontendOptions);
        var branches = new BranchService(
            new BranchRepository(context),
            normalizer,
            tokens,
            invitations,
            clock);
        var position = JobPosition.Create(organizationA.Id, "Administrador", "ADMINISTRADOR", now);
        context.Add(position);
        await context.SaveChangesAsync();
        var employees = new EmployeeService(
            new EmployeeRepository(context),
            new BranchRepository(context),
            normalizer,
            passwords,
            tokens,
            invitations,
            clock);
        var authenticationRepository = new AuthenticationRepository(context);
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
        return new TestServices(
            branches,
            employees,
            position.Id,
            invitations,
            authentication,
            email,
            new CurrentAccount(superA.Id, AccountType.Tenant, organizationA.Id, null, [SystemRoleCodes.SuperAdmin]),
            new CurrentAccount(superB.Id, AccountType.Tenant, organizationB.Id, null, [SystemRoleCodes.SuperAdmin]));
    }

    private static BranchInput BranchInput(string name) =>
        new(name, "sucursal@legaria.test", "+573001112233", "Carrera 7 # 10-20", "11001");

    private static Task<EmployeeResult> CreateAdministratorAsync(
        TestServices services,
        Guid assignmentBranchId,
        IReadOnlyCollection<Guid> accessBranchIds,
        string email = "branch.admin@legaria.test") =>
        services.Employees.CreateAsync(
            assignmentBranchId,
            new CreateEmployeeInput(
                "CC",
                email.ToUpperInvariant(),
                "Brenda",
                "Administradora",
                services.PositionId,
                new DateOnly(2026, 8, 4),
                true,
                new AdministrativeAccessInput(email, accessBranchIds)),
            services.SuperActor,
            Client(),
            CancellationToken.None);

    private static ClientContext Client() => new("127.0.0.1", "branch-tests");

    private static string ExtractToken(string html)
    {
        var uri = new Uri(html);
        return Uri.UnescapeDataString(uri.Query["?token=".Length..]);
    }

    private sealed record TestServices(
        BranchService Branches,
        EmployeeService Employees,
        Guid PositionId,
        TenantInvitationService Invitations,
        AuthenticationService Authentication,
        CapturingEmailSender Email,
        CurrentAccount SuperActor,
        CurrentAccount OtherSuperActor);

    private sealed class FixedClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow { get; } = now;
    }

    private sealed class CapturingEmailSender : IEmailSender
    {
        public string LastHtml { get; private set; } = string.Empty;

        public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
        {
            LastHtml = message.HtmlBody;
            return Task.CompletedTask;
        }
    }

    private sealed class PassThroughRenderer : IEmailTemplateRenderer
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
