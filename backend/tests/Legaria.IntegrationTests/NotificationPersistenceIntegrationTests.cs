using Legaria.Application.Authentication;
using Legaria.Application.Notifications;
using Legaria.Domain.Authentication;
using Legaria.Domain.Documents;
using Legaria.Domain.Notifications;
using Legaria.Domain.Tenancy;
using Legaria.Infrastructure.Persistence;
using Legaria.Infrastructure.Authentication;
using Microsoft.EntityFrameworkCore;

namespace Legaria.IntegrationTests;

[Collection(PostgreSqlCollection.Name)]
public sealed class NotificationPersistenceIntegrationTests(PostgreSqlFixture fixture)
{
    [Fact]
    public async Task NotificationSchemaEnforcesGlobalChannelAndTenantReferences()
    {
        await fixture.ResetAsync();
        var now = new DateTimeOffset(2026, 8, 5, 18, 0, 0, TimeSpan.Zero);
        await using var context = fixture.CreateDbContext();
        var first = Organization.Create("Primera", "Primera S.A.S.", "900373913", 4, "a@test.local", "+573001111111", "Calle 1", "11001", now);
        var second = Organization.Create("Segunda", "Segunda S.A.S.", "800197268", 4, "b@test.local", "+573002222222", "Calle 2", "11001", now);
        context.AddRange(first, second);
        var firstChannel = WhatsAppChannel.Create(first.Id, "Principal", "PRINCIPAL", "phone-1", "business-1", "encrypted", "hash-1", "secret", now);
        context.Add(firstChannel);
        await context.SaveChangesAsync();

        context.Add(WhatsAppChannel.Create(second.Id, "Otro", "OTRO", "phone-1", "business-2", "encrypted", "hash-2", "secret", now));
        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        context.ChangeTracker.Clear();

        var category = DocumentCategory.Create(second.Id, "Licencias", "LICENCIAS", null, DocumentScope.Employee, now);
        var type = DocumentType.Create(second.Id, category.Id, "SOAT", "SOAT", null, true,
            DocumentDateMode.Optional, DocumentDateMode.Required, false, true, ["PDF"], now);
        context.AddRange(category, type);
        await context.SaveChangesAsync();

        var template = WhatsAppTemplate.Create(first.Id, firstChannel.Id, "meta-1", "vence_documento", "UTILITY", "es_CO",
            "APPROVED", "[]", "[]", "[]", "hash", now);
        context.Add(template);
        await context.SaveChangesAsync();
        context.Add(NotificationRule.Create(first.Id, "SOAT", "SOAT", type.Id, firstChannel.Id, template.Id,
            "NORMAL", "[\"EMPLOYEE\"]", "{}", template.ContentHash, now));

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task BranchAdministratorCanOnlyManageOwnContact()
    {
        await fixture.ResetAsync();
        var now = new DateTimeOffset(2026, 8, 5, 18, 0, 0, TimeSpan.Zero);
        await using var context = fixture.CreateDbContext();
        var organization = Organization.Create("Primera", "Primera S.A.S.", "900373913", 4,
            "a@test.local", "+573001111111", "Calle 1", "11001", now);
        var account = UserAccount.Create(organization.Id, null, "admin@test.local", "ADMIN@TEST.LOCAL",
            "hash", "Ana", "Admin", "stamp", true, now);
        context.AddRange(organization, account);
        await context.SaveChangesAsync();
        var service = new NotificationService(new NotificationRepository(context), new FakeCloudClient(),
            new PassThroughProtector(), new SecureTokenService(), new FixedClock(now));
        var actor = new CurrentAccount(account.Id, AccountType.Tenant, organization.Id, null,
            [SystemRoleCodes.BranchAdmin]);

        var forbidden = await Assert.ThrowsAsync<NotificationException>(() => service.ListChannelsAsync(actor, CancellationToken.None));
        var contact = await service.UpdateContactAsync(new("+573001234567", true), actor, CancellationToken.None);

        Assert.Equal(NotificationErrorCodes.Forbidden, forbidden.Code);
        Assert.Equal("+573001234567", contact.MobilePhone);
        Assert.Equal(now, contact.WhatsAppConsentAt);
    }

    private sealed class FixedClock(DateTimeOffset now) : IClock { public DateTimeOffset UtcNow { get; } = now; }
    private sealed class PassThroughProtector : IIntegrationSecretProtector
    {
        public string Protect(string value) => value;
        public string Unprotect(string value) => value;
    }
    private sealed class FakeCloudClient : IWhatsAppCloudClient
    {
        public Task<MetaConnectionResult> TestConnectionAsync(string phoneNumberId, string businessAccountId, string accessToken, CancellationToken cancellationToken) =>
            Task.FromResult(new MetaConnectionResult(true, "+573001234567", null));
        public Task<MetaTemplateSyncResult> GetTemplatesAsync(string businessAccountId, string accessToken, CancellationToken cancellationToken) =>
            Task.FromResult(new MetaTemplateSyncResult(true, [], null));
        public Task<MetaTemplateSendResult> SendTemplateAsync(string phoneNumberId, string accessToken, string destination,
            string templateName, string language, string componentsJson, IReadOnlyDictionary<string, string> mappings,
            IReadOnlyDictionary<string, string> values, CancellationToken cancellationToken) =>
            Task.FromResult(new MetaTemplateSendResult(true, false, "message", "{}", "{}", null, null, null));
    }
}
