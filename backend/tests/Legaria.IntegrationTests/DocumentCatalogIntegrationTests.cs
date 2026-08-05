using Legaria.Application.Authentication;
using Legaria.Application.Documents;
using Legaria.Domain.Authentication;
using Legaria.Domain.Documents;
using Legaria.Domain.Tenancy;
using Legaria.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Legaria.IntegrationTests;

[Collection(PostgreSqlCollection.Name)]
public sealed class DocumentCatalogIntegrationTests(PostgreSqlFixture fixture)
{
    [Fact]
    public async Task CatalogSupportsAlphabeticalLifecycleAndMovingTypesWithinScope()
    {
        await fixture.ResetAsync();
        await using var context = fixture.CreateDbContext();
        var setup = await CreateSetupAsync(context);
        var service = new DocumentCatalogService(new DocumentCatalogRepository(context), new FixedClock());
        var social = await service.CreateCategoryAsync(
            new DocumentCategoryInput("Seguridad Social", "Afiliaciones", "EMPLOYEE"), setup.SuperActor, CancellationToken.None);
        var exams = await service.CreateCategoryAsync(
            new DocumentCategoryInput("Exámenes médicos", null, "EMPLOYEE"), setup.SuperActor, CancellationToken.None);

        var categories = await service.ListCategoriesAsync("EMPLOYEE", "ALL", null, setup.SuperActor, CancellationToken.None);
        Assert.Equal(["Exámenes médicos", "Seguridad Social"], categories.Select(item => item.Name));
        Assert.Equal(
            DocumentCatalogErrorCodes.CategoryDuplicateName,
            (await Assert.ThrowsAsync<DocumentCatalogException>(() => service.CreateCategoryAsync(
                new DocumentCategoryInput("  seguridad   social ", null, "EMPLOYEE"), setup.SuperActor, CancellationToken.None))).Code);

        var affiliation = await service.CreateTypeAsync(
            TypeInput(social.Id, "Afiliación EPS"), setup.SuperActor, CancellationToken.None);
        Assert.Equal(
            DocumentCatalogErrorCodes.TypeDuplicateName,
            (await Assert.ThrowsAsync<DocumentCatalogException>(() => service.CreateTypeAsync(
                TypeInput(social.Id, " afiliación   eps "), setup.SuperActor, CancellationToken.None))).Code);
        var moved = await service.UpdateTypeAsync(
            affiliation.Id,
            TypeInput(exams.Id, "Examen periódico"),
            setup.SuperActor,
            CancellationToken.None);
        Assert.Equal(exams.Id, moved.CategoryId);
        Assert.Equal(["IMAGE", "LINK", "PDF"], moved.AllowedEvidenceKinds.OrderBy(item => item));
        Assert.Equal("INACTIVE", (await service.DeactivateTypeAsync(moved.Id, setup.SuperActor, CancellationToken.None)).Status);
        Assert.Equal("ACTIVE", (await service.ReactivateTypeAsync(moved.Id, setup.SuperActor, CancellationToken.None)).Status);

        await service.DeactivateCategoryAsync(social.Id, setup.SuperActor, CancellationToken.None);
        Assert.Equal(
            DocumentCatalogErrorCodes.InactiveCategory,
            (await Assert.ThrowsAsync<DocumentCatalogException>(() => service.UpdateTypeAsync(
                moved.Id,
                TypeInput(social.Id, "Afiliación ARL"),
                setup.SuperActor,
                CancellationToken.None))).Code);
        var inactiveCategory = Assert.Single(await service.ListCategoriesAsync("EMPLOYEE", "INACTIVE", null, setup.SuperActor, CancellationToken.None));
        Assert.Equal(social.Id, inactiveCategory.Id);
        Assert.Equal("ACTIVE", (await service.ReactivateCategoryAsync(social.Id, setup.SuperActor, CancellationToken.None)).Status);
    }

    [Fact]
    public async Task BranchAdministratorOnlyMutatesBranchCatalogAndTenantIsolationIsEnforced()
    {
        await fixture.ResetAsync();
        await using var context = fixture.CreateDbContext();
        var setup = await CreateSetupAsync(context);
        var service = new DocumentCatalogService(new DocumentCatalogRepository(context), new FixedClock());
        var employeeCategory = await service.CreateCategoryAsync(
            new DocumentCategoryInput("Identidad", null, "EMPLOYEE"), setup.SuperActor, CancellationToken.None);
        var otherTenantCategory = await service.CreateCategoryAsync(
            new DocumentCategoryInput("Actas", null, "BRANCH"), setup.OtherSuperActor, CancellationToken.None);

        var branchCategory = await service.CreateCategoryAsync(
            new DocumentCategoryInput("Actas", null, "BRANCH"), setup.BranchActor, CancellationToken.None);
        Assert.Equal("BRANCH", branchCategory.Scope);
        Assert.Equal(
            "Planilla de aseo",
            (await service.CreateTypeAsync(TypeInput(branchCategory.Id, "Planilla de aseo"), setup.BranchActor, CancellationToken.None)).Name);
        Assert.Contains(
            await service.ListCategoriesAsync("ALL", "ALL", null, setup.BranchActor, CancellationToken.None),
            item => item.Id == employeeCategory.Id);
        Assert.Equal(
            DocumentCatalogErrorCodes.Forbidden,
            (await Assert.ThrowsAsync<DocumentCatalogException>(() => service.UpdateCategoryAsync(
                employeeCategory.Id,
                new UpdateDocumentCategoryInput("Identificación", null),
                setup.BranchActor,
                CancellationToken.None))).Code);
        Assert.Equal(
            DocumentCatalogErrorCodes.Forbidden,
            (await Assert.ThrowsAsync<DocumentCatalogException>(() => service.CreateTypeAsync(
                TypeInput(employeeCategory.Id, "Cédula"), setup.BranchActor, CancellationToken.None))).Code);
        Assert.Equal(
            DocumentCatalogErrorCodes.CategoryNotFound,
            (await Assert.ThrowsAsync<DocumentCatalogException>(() => service.UpdateCategoryAsync(
                otherTenantCategory.Id,
                new UpdateDocumentCategoryInput("Oculta", null),
                setup.SuperActor,
                CancellationToken.None))).Code);
    }

    [Fact]
    public async Task InvalidEvidenceModesCrossScopeMovesAndDatabaseDuplicatesAreRejected()
    {
        await fixture.ResetAsync();
        await using var context = fixture.CreateDbContext();
        var setup = await CreateSetupAsync(context);
        var service = new DocumentCatalogService(new DocumentCatalogRepository(context), new FixedClock());
        var employee = await service.CreateCategoryAsync(
            new DocumentCategoryInput("Cursos", null, "EMPLOYEE"), setup.SuperActor, CancellationToken.None);
        var branch = await service.CreateCategoryAsync(
            new DocumentCategoryInput("Planillas", null, "BRANCH"), setup.SuperActor, CancellationToken.None);

        Assert.Equal(
            DocumentCatalogErrorCodes.InvalidData,
            (await Assert.ThrowsAsync<DocumentCatalogException>(() => service.CreateTypeAsync(
                TypeInput(employee.Id, "Curso", []), setup.SuperActor, CancellationToken.None))).Code);
        Assert.Equal(
            DocumentCatalogErrorCodes.InvalidData,
            (await Assert.ThrowsAsync<DocumentCatalogException>(() => service.CreateTypeAsync(
                TypeInput(employee.Id, "Curso", ["EXECUTABLE"]), setup.SuperActor, CancellationToken.None))).Code);
        var course = await service.CreateTypeAsync(TypeInput(employee.Id, "Curso"), setup.SuperActor, CancellationToken.None);
        Assert.Equal(
            DocumentCatalogErrorCodes.ScopeMismatch,
            (await Assert.ThrowsAsync<DocumentCatalogException>(() => service.UpdateTypeAsync(
                course.Id,
                TypeInput(branch.Id, "Curso"),
                setup.SuperActor,
                CancellationToken.None))).Code);

        context.DocumentCategories.Add(DocumentCategory.Create(
            setup.OrganizationId,
            " cursos ",
            "CURSOS",
            null,
            DocumentScope.Employee,
            new FixedClock().UtcNow));
        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    private static DocumentTypeInput TypeInput(Guid categoryId, string name, IReadOnlyCollection<string>? evidenceKinds = null) =>
        new(categoryId, name, "Configuración documental", true, "OPTIONAL", "REQUIRED", true, true, evidenceKinds ?? ["PDF", "IMAGE", "LINK"]);

    private static async Task<TestSetup> CreateSetupAsync(LegariaDbContext context)
    {
        var now = new FixedClock().UtcNow;
        var first = Organization.Create("Organización A", "Organización A S.A.S.", "900373913", 4, "a@legaria.test", "+573001111111", "Calle 1", "11001", now);
        var second = Organization.Create("Organización B", "Organización B S.A.S.", "800197268", 4, "b@legaria.test", "+573002222222", "Calle 2", "11001", now);
        context.AddRange(first, second);
        await context.SaveChangesAsync();
        return new TestSetup(
            first.Id,
            new CurrentAccount(Guid.NewGuid(), AccountType.Tenant, first.Id, null, [SystemRoleCodes.SuperAdmin]),
            new CurrentAccount(Guid.NewGuid(), AccountType.Tenant, second.Id, null, [SystemRoleCodes.SuperAdmin]),
            new CurrentAccount(Guid.NewGuid(), AccountType.Tenant, first.Id, Guid.NewGuid(), [SystemRoleCodes.BranchAdmin]));
    }

    private sealed record TestSetup(Guid OrganizationId, CurrentAccount SuperActor, CurrentAccount OtherSuperActor, CurrentAccount BranchActor);

    private sealed class FixedClock : IClock
    {
        public DateTimeOffset UtcNow { get; } = new(2026, 8, 5, 18, 0, 0, TimeSpan.Zero);
    }
}
