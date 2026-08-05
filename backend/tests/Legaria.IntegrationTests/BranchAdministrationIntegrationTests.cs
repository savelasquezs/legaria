using Legaria.Application.Authentication;
using Legaria.Application.Branches;
using Legaria.Application.Configuration;
using Legaria.Application.Documents;
using Legaria.Application.Employees;
using Legaria.Application.Organizations;
using Legaria.Domain.Authentication;
using Legaria.Domain.Documents;
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
                new DateOnly(2026, 8, 4),
                false,
                null),
            services.SuperActor,
            Client(),
            CancellationToken.None);

        Assert.Null(assigned.AdministrativeAccess);
        Assert.Equal(2, assigned.EmploymentRelationships.Single().Assignments.Count);
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

    [Fact]
    public async Task BranchAdministratorSeesOtherWorkersOnlyInsideAssignedBranches()
    {
        await fixture.ResetAsync();
        await using var context = fixture.CreateDbContext();
        var services = await CreateServicesAsync(context);
        var assignedBranch = await services.Branches.CreateBranchAsync(
            BranchInput("Centro"), services.SuperActor, Client(), CancellationToken.None);
        var hiddenBranch = await services.Branches.CreateBranchAsync(
            BranchInput("Norte"), services.SuperActor, Client(), CancellationToken.None);
        var administrator = await CreateAdministratorAsync(services, assignedBranch.Id, [assignedBranch.Id]);
        var colleague = await services.Employees.CreateAsync(
            assignedBranch.Id,
            new CreateEmployeeInput("CC", "1030999999", "Carlos", "Compañero", services.PositionId, new DateOnly(2026, 8, 4), true, null),
            services.SuperActor,
            Client(),
            CancellationToken.None);
        await services.Employees.AssignAsync(
            hiddenBranch.Id,
            colleague.Id,
            new AssignEmployeeInput(services.PositionId, new DateOnly(2026, 8, 4), false, null),
            services.SuperActor,
            Client(),
            CancellationToken.None);
        var hiddenWorker = await services.Employees.CreateAsync(
            hiddenBranch.Id,
            new CreateEmployeeInput("CC", "1030888777", "Helena", "Oculta", services.PositionId, new DateOnly(2026, 8, 4), true, null),
            services.SuperActor,
            Client(),
            CancellationToken.None);
        var actor = new CurrentAccount(
            administrator.AdministrativeAccess!.AccountId,
            AccountType.Tenant,
            services.SuperActor.OrganizationId,
            administrator.Id,
            [SystemRoleCodes.BranchAdmin]);

        var page = await services.Employees.ListAsync(
            1, 20, null, assignedBranch.Id, null, actor, CancellationToken.None);
        var visible = Assert.Single(page.Items);
        Assert.Equal(colleague.Id, visible.Id);
        Assert.Null(visible.AdministrativeAccess);
        Assert.Equal(assignedBranch.Id, Assert.Single(visible.Assignments).BranchId);

        var detail = await services.Employees.GetAsync(colleague.Id, actor, CancellationToken.None);
        Assert.Equal(assignedBranch.Id, Assert.Single(Assert.Single(detail.EmploymentRelationships).Assignments).BranchId);
        Assert.Equal(
            EmployeeErrorCodes.Forbidden,
            (await Assert.ThrowsAsync<EmployeeException>(() => services.Employees.GetAsync(
                administrator.Id, actor, CancellationToken.None))).Code);
        Assert.Equal(
            EmployeeErrorCodes.NotFound,
            (await Assert.ThrowsAsync<EmployeeException>(() => services.Employees.GetAsync(
                hiddenWorker.Id, actor, CancellationToken.None))).Code);
        Assert.Equal(
            EmployeeErrorCodes.NotFound,
            (await Assert.ThrowsAsync<EmployeeException>(() => services.Employees.ListAsync(
                1, 20, null, hiddenBranch.Id, null, actor, CancellationToken.None))).Code);
    }

    [Fact]
    public async Task JobPositionsCanBeManagedWithoutLosingHistory()
    {
        await fixture.ResetAsync();
        await using var context = fixture.CreateDbContext();
        var services = await CreateServicesAsync(context);

        var created = await services.Employees.CreateJobPositionAsync(
            new JobPositionInput("  Auxiliar   contable  "),
            services.SuperActor,
            CancellationToken.None);
        Assert.Equal("Auxiliar contable", created.Name);
        var duplicate = await Assert.ThrowsAsync<EmployeeException>(() => services.Employees.CreateJobPositionAsync(
            new JobPositionInput("auxiliar contable"),
            services.SuperActor,
            CancellationToken.None));
        Assert.Equal(EmployeeErrorCodes.JobPositionDuplicateName, duplicate.Code);
        var crossTenant = await Assert.ThrowsAsync<EmployeeException>(() => services.Employees.UpdateJobPositionAsync(
            created.Id,
            new JobPositionInput("Otro"),
            services.OtherSuperActor,
            CancellationToken.None));
        Assert.Equal(EmployeeErrorCodes.JobPositionNotFound, crossTenant.Code);

        var updated = await services.Employees.UpdateJobPositionAsync(
            created.Id,
            new JobPositionInput("Analista contable"),
            services.SuperActor,
            CancellationToken.None);
        Assert.Equal("Analista contable", updated.Name);

        var inactive = await services.Employees.DeactivateJobPositionAsync(
            created.Id,
            services.SuperActor,
            CancellationToken.None);
        Assert.Equal("INACTIVE", inactive.Status);
        Assert.Contains(
            await services.Employees.ListJobPositionsAsync("INACTIVE", services.SuperActor, CancellationToken.None),
            item => item.Id == created.Id);

        var reactivated = await services.Employees.ReactivateJobPositionAsync(
            created.Id,
            services.SuperActor,
            CancellationToken.None);
        Assert.Equal("ACTIVE", reactivated.Status);
    }

    [Fact]
    public async Task JobPositionDocumentRequirementsAreTenantScopedAndOnlyAcceptAvailableEmployeeTypes()
    {
        await fixture.ResetAsync();
        await using var context = fixture.CreateDbContext();
        var services = await CreateServicesAsync(context);
        var organizationId = services.SuperActor.OrganizationId!.Value;
        var now = new DateTimeOffset(2026, 8, 4, 20, 0, 0, TimeSpan.Zero);
        var employeeCategory = DocumentCategory.Create(
            organizationId, "Identidad", "IDENTIDAD", null, DocumentScope.Employee, now);
        var inactiveCategory = DocumentCategory.Create(
            organizationId, "Cursos", "CURSOS", null, DocumentScope.Employee, now);
        inactiveCategory.Deactivate(now);
        var branchCategory = DocumentCategory.Create(
            organizationId, "Actas", "ACTAS", null, DocumentScope.Branch, now);
        var employeeType = DocumentType.Create(
            organizationId, employeeCategory.Id, "Cédula", "CÉDULA", null, false,
            DocumentDateMode.Never, DocumentDateMode.Never, false, false, [DocumentEvidenceKinds.Pdf], now);
        var unavailableType = DocumentType.Create(
            organizationId, inactiveCategory.Id, "Curso", "CURSO", null, false,
            DocumentDateMode.Optional, DocumentDateMode.Required, false, false, [DocumentEvidenceKinds.Pdf], now);
        var branchType = DocumentType.Create(
            organizationId, branchCategory.Id, "Acta", "ACTA", null, false,
            DocumentDateMode.Optional, DocumentDateMode.Never, false, false, [DocumentEvidenceKinds.Pdf], now);
        context.AddRange(employeeCategory, inactiveCategory, branchCategory, employeeType, unavailableType, branchType);
        await context.SaveChangesAsync();

        var saved = await services.Employees.UpdateJobPositionDocumentRequirementsAsync(
            services.PositionId,
            new JobPositionDocumentRequirementsInput([employeeType.Id, employeeType.Id]),
            services.SuperActor,
            CancellationToken.None);

        Assert.Equal([employeeType.Id], saved.DocumentTypeIds);
        Assert.Equal(
            [employeeType.Id],
            (await services.Employees.GetJobPositionDocumentRequirementsAsync(
                services.PositionId,
                services.SuperActor,
                CancellationToken.None)).DocumentTypeIds);
        Assert.Equal(
            1,
            Assert.Single(await services.Employees.ListJobPositionsAsync(
                "ALL",
                services.SuperActor,
                CancellationToken.None),
                item => item.Id == services.PositionId).RequiredDocumentCount);
        Assert.Equal(
            [employeeType.Id],
            (await services.Employees.UpdateJobPositionDocumentRequirementsAsync(
                services.PositionId,
                new JobPositionDocumentRequirementsInput([employeeType.Id]),
                services.SuperActor,
                CancellationToken.None)).DocumentTypeIds);

        foreach (var invalidTypeId in new[] { unavailableType.Id, branchType.Id, Guid.NewGuid() })
        {
            var invalid = await Assert.ThrowsAsync<EmployeeException>(() =>
                services.Employees.UpdateJobPositionDocumentRequirementsAsync(
                    services.PositionId,
                    new JobPositionDocumentRequirementsInput([invalidTypeId]),
                    services.SuperActor,
                    CancellationToken.None));
            Assert.Equal(EmployeeErrorCodes.InvalidDocumentRequirement, invalid.Code);
        }

        Assert.Equal(
            [employeeType.Id],
            (await services.Employees.GetJobPositionDocumentRequirementsAsync(
                services.PositionId,
                services.SuperActor,
                CancellationToken.None)).DocumentTypeIds);

        Assert.Empty((await services.Employees.UpdateJobPositionDocumentRequirementsAsync(
            services.PositionId,
            new JobPositionDocumentRequirementsInput([]),
            services.SuperActor,
            CancellationToken.None)).DocumentTypeIds);

        var crossTenant = await Assert.ThrowsAsync<EmployeeException>(() =>
            services.Employees.GetJobPositionDocumentRequirementsAsync(
                services.PositionId,
                services.OtherSuperActor,
                CancellationToken.None));
        Assert.Equal(EmployeeErrorCodes.JobPositionNotFound, crossTenant.Code);
        var branchActor = new CurrentAccount(
            Guid.NewGuid(),
            AccountType.Tenant,
            organizationId,
            Guid.NewGuid(),
            [SystemRoleCodes.BranchAdmin]);
        Assert.Equal(
            EmployeeErrorCodes.Forbidden,
            (await Assert.ThrowsAsync<EmployeeException>(() =>
                services.Employees.GetJobPositionDocumentRequirementsAsync(
                    services.PositionId,
                    branchActor,
                    CancellationToken.None))).Code);
    }

    [Fact]
    public async Task EmployeeDocumentSummaryCombinesPositionRequirementsAndUpcomingExpirations()
    {
        await fixture.ResetAsync();
        await using var context = fixture.CreateDbContext();
        var services = await CreateServicesAsync(context);
        var organizationId = services.SuperActor.OrganizationId!.Value;
        var now = new DateTimeOffset(2026, 8, 4, 20, 0, 0, TimeSpan.Zero);
        var category = DocumentCategory.Create(organizationId, "Licencias", "LICENCIAS", null, DocumentScope.Employee, now);
        var documentType = DocumentType.Create(organizationId, category.Id, "Licencia", "LICENCIA", null, false,
            DocumentDateMode.Optional, DocumentDateMode.Required, false, false, [DocumentEvidenceKinds.Pdf], now);
        context.AddRange(category, documentType, JobPositionDocumentRequirement.Create(organizationId, services.PositionId, documentType.Id));
        await context.SaveChangesAsync();
        var branch = await services.Branches.CreateBranchAsync(BranchInput("Centro"), services.SuperActor, Client(), CancellationToken.None);
        var employee = await services.Employees.CreateAsync(branch.Id,
            new CreateEmployeeInput("CC", "1030999999", "Laura", "Documentada", services.PositionId, new DateOnly(2026, 8, 4), true, null),
            services.SuperActor, Client(), CancellationToken.None);
        var storage = new MemoryDocumentStorage();
        var documentService = new EmployeeDocumentService(
            new EmployeeDocumentRepository(context), new EmployeeRepository(context), new BranchRepository(context), storage, new FixedClock(now));

        var missing = await documentService.GetSummaryAsync(employee.Id, services.SuperActor, CancellationToken.None);
        Assert.Equal(1, missing.MissingCount);
        Assert.Equal("Licencias", Assert.Single(missing.Categories).Name);

        await using var content = new MemoryStream("%PDF-1.7 evidence"u8.ToArray());
        var completed = await documentService.UploadAsync(employee.Id,
            new UploadEmployeeDocumentInput(documentType.Id, new DateOnly(2026, 8, 1), new DateOnly(2026, 9, 1),
                [new EmployeeDocumentFileInput("licencia.pdf", "application/pdf", content.Length, content)], []),
            services.SuperActor, CancellationToken.None);

        Assert.Equal(0, completed.MissingCount);
        Assert.Equal(new DateOnly(2026, 9, 1), Assert.Single(completed.UpcomingExpirations).ExpiresOn);
        Assert.Single(storage.Objects);
    }

    [Fact]
    public async Task EndingRelationshipClosesAssignmentsAndSuspendsLinkedAdministrator()
    {
        await fixture.ResetAsync();
        await using var context = fixture.CreateDbContext();
        var services = await CreateServicesAsync(context);
        var branch = await services.Branches.CreateBranchAsync(
            BranchInput("Centro"), services.SuperActor, Client(), CancellationToken.None);
        var created = await CreateAdministratorAsync(services, branch.Id, [branch.Id]);
        var relationship = Assert.Single(created.EmploymentRelationships);
        var accountId = Assert.IsType<EmployeeAdministrativeAccessResult>(created.AdministrativeAccess).AccountId;

        var ended = await services.Employees.EndRelationshipAsync(
            created.Id,
            relationship.Id,
            new EndEmploymentRelationshipInput(new DateOnly(2026, 8, 4)),
            services.SuperActor,
            Client(),
            CancellationToken.None);

        Assert.Equal("ENDED", Assert.Single(ended.EmploymentRelationships).Status);
        Assert.All(ended.EmploymentRelationships.Single().Assignments, item => Assert.Equal("ENDED", item.Status));
        Assert.Equal(AccountStatus.Suspended, (await context.UserAccounts.SingleAsync(item => item.Id == accountId)).Status);
        Assert.All(
            await context.AccountTokens.Where(item => item.UserAccountId == accountId && item.Purpose == AccountTokenPurpose.TenantInvitation).ToArrayAsync(),
            item => Assert.NotNull(item.RevokedAt));
    }

    [Fact]
    public async Task AssignmentTransitionPreservesPeriodsAndPrimaryCanMove()
    {
        await fixture.ResetAsync();
        await using var context = fixture.CreateDbContext();
        var services = await CreateServicesAsync(context);
        var first = await services.Branches.CreateBranchAsync(
            BranchInput("Centro"), services.SuperActor, Client(), CancellationToken.None);
        var second = await services.Branches.CreateBranchAsync(
            BranchInput("Norte"), services.SuperActor, Client(), CancellationToken.None);
        var replacementPosition = await services.Employees.CreateJobPositionAsync(
            new JobPositionInput("Coordinador"), services.SuperActor, CancellationToken.None);
        var created = await services.Employees.CreateAsync(
            first.Id,
            new CreateEmployeeInput("CC", "1030555555", "Lina", "García", services.PositionId, new DateOnly(2026, 8, 3), true, null),
            services.SuperActor,
            Client(),
            CancellationToken.None);
        var original = Assert.Single(created.EmploymentRelationships.Single().Assignments);

        var transitioned = await services.Employees.TransitionAssignmentAsync(
            created.Id,
            original.Id,
            new TransitionEmployeeAssignmentInput(first.Id, replacementPosition.Id, new DateOnly(2026, 8, 4)),
            services.SuperActor,
            Client(),
            CancellationToken.None);
        var assignments = transitioned.EmploymentRelationships.Single().Assignments;
        Assert.Contains(assignments, item => item.Id == original.Id && item.EndedOn == new DateOnly(2026, 8, 3));
        Assert.Contains(assignments, item => item.JobPositionId == replacementPosition.Id && item.StartedOn == new DateOnly(2026, 8, 4) && item.IsPrimary);

        var withSecond = await services.Employees.AssignAsync(
            second.Id,
            created.Id,
            new AssignEmployeeInput(replacementPosition.Id, new DateOnly(2026, 8, 4), false, null),
            services.SuperActor,
            Client(),
            CancellationToken.None);
        var secondary = withSecond.EmploymentRelationships.Single().Assignments.Single(item => item.BranchId == second.Id);
        var changed = await services.Employees.MakePrimaryAssignmentAsync(
            created.Id,
            secondary.Id,
            services.SuperActor,
            Client(),
            CancellationToken.None);
        Assert.True(changed.EmploymentRelationships.Single().Assignments.Single(item => item.Id == secondary.Id).IsPrimary);
        Assert.Single(changed.EmploymentRelationships.Single().Assignments.Where(item => item.Status == "ACTIVE" && item.IsPrimary));
    }

    [Fact]
    public async Task FutureEmploymentDatesAreRejected()
    {
        await fixture.ResetAsync();
        await using var context = fixture.CreateDbContext();
        var services = await CreateServicesAsync(context);
        var branch = await services.Branches.CreateBranchAsync(
            BranchInput("Centro"), services.SuperActor, Client(), CancellationToken.None);

        var exception = await Assert.ThrowsAsync<EmployeeException>(() => services.Employees.CreateAsync(
            branch.Id,
            new CreateEmployeeInput("CC", "1030666666", "Mario", "Futuro", services.PositionId, new DateOnly(2026, 8, 5), true, null),
            services.SuperActor,
            Client(),
            CancellationToken.None));

        Assert.Equal(EmployeeErrorCodes.InvalidDate, exception.Code);
    }

    [Fact]
    public async Task DatabaseRejectsDuplicateActiveRelationshipsAndBranchAssignments()
    {
        await fixture.ResetAsync();
        await using (var relationshipContext = fixture.CreateDbContext())
        {
            var services = await CreateServicesAsync(relationshipContext);
            var branch = await services.Branches.CreateBranchAsync(
                BranchInput("Centro"), services.SuperActor, Client(), CancellationToken.None);
            var created = await services.Employees.CreateAsync(
                branch.Id,
                new CreateEmployeeInput("CC", "1030777777", "Rosa", "Duplicada", services.PositionId, new DateOnly(2026, 8, 4), true, null),
                services.SuperActor,
                Client(),
                CancellationToken.None);
            relationshipContext.Add(EmploymentRelationship.Create(
                services.SuperActor.OrganizationId!.Value,
                created.Id,
                new DateOnly(2026, 8, 4),
                new DateTimeOffset(2026, 8, 4, 20, 0, 0, TimeSpan.Zero)));

            await Assert.ThrowsAsync<DbUpdateException>(() => relationshipContext.SaveChangesAsync());
        }

        await fixture.ResetAsync();
        await using var assignmentContext = fixture.CreateDbContext();
        var assignmentServices = await CreateServicesAsync(assignmentContext);
        var assignmentBranch = await assignmentServices.Branches.CreateBranchAsync(
            BranchInput("Centro"), assignmentServices.SuperActor, Client(), CancellationToken.None);
        var assignmentEmployee = await assignmentServices.Employees.CreateAsync(
            assignmentBranch.Id,
            new CreateEmployeeInput("CC", "1030888888", "Sara", "Duplicada", assignmentServices.PositionId, new DateOnly(2026, 8, 4), true, null),
            assignmentServices.SuperActor,
            Client(),
            CancellationToken.None);
        var relationship = Assert.Single(assignmentEmployee.EmploymentRelationships);
        assignmentContext.Add(EmployeeAssignment.Create(
            assignmentServices.SuperActor.OrganizationId!.Value,
            relationship.Id,
            assignmentBranch.Id,
            assignmentServices.PositionId,
            false,
            new DateOnly(2026, 8, 4),
            new DateTimeOffset(2026, 8, 4, 20, 0, 0, TimeSpan.Zero)));

        await Assert.ThrowsAsync<DbUpdateException>(() => assignmentContext.SaveChangesAsync());
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
            branches,
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

    private static Task<EmployeeDetailResult> CreateAdministratorAsync(
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

    private sealed class MemoryDocumentStorage : IEmployeeDocumentStorage
    {
        public Dictionary<string, byte[]> Objects { get; } = [];
        public async Task<string> UploadAsync(Stream content, string extension, string contentType, CancellationToken cancellationToken)
        {
            var name = $"private/{Guid.NewGuid():N}{extension}";
            using var memory = new MemoryStream();
            await content.CopyToAsync(memory, cancellationToken);
            Objects[name] = memory.ToArray();
            return name;
        }
        public Task<Stream> DownloadAsync(string objectName, CancellationToken cancellationToken) => Task.FromResult<Stream>(new MemoryStream(Objects[objectName]));
        public Task DeleteAsync(string objectName, CancellationToken cancellationToken) { Objects.Remove(objectName); return Task.CompletedTask; }
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
