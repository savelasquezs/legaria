using Legaria.Application.Notifications;
using Legaria.Domain.Authentication;
using Legaria.Domain.Documents;
using Legaria.Domain.Notifications;
using Legaria.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Legaria.Infrastructure.Persistence;

public sealed class NotificationRepository(LegariaDbContext db) : INotificationRepository
{
    public async Task<IReadOnlyCollection<WhatsAppChannel>> ListChannelsAsync(Guid organizationId, CancellationToken ct) =>
        await db.WhatsAppChannels.AsNoTracking().Where(x => x.OrganizationId == organizationId).OrderBy(x => x.Name).ToArrayAsync(ct);
    public Task<WhatsAppChannel?> FindChannelAsync(Guid organizationId, Guid id, CancellationToken ct) =>
        db.WhatsAppChannels.FirstOrDefaultAsync(x => x.OrganizationId == organizationId && x.Id == id, ct);
    public Task<bool> ChannelNameExistsAsync(Guid organizationId, string normalizedName, Guid? excludingId, CancellationToken ct) =>
        db.WhatsAppChannels.AnyAsync(x => x.OrganizationId == organizationId && x.NormalizedName == normalizedName && x.Id != excludingId, ct);
    public Task<bool> PhoneNumberExistsAsync(string phoneNumberId, Guid? excludingId, CancellationToken ct) =>
        db.WhatsAppChannels.AnyAsync(x => x.PhoneNumberId == phoneNumberId && x.Id != excludingId, ct);
    public Task<bool> VerifyHashExistsAsync(string hash, Guid? excludingId, CancellationToken ct) =>
        db.WhatsAppChannels.AnyAsync(x => x.WebhookVerifyTokenHash == hash && x.Id != excludingId, ct);
    public async Task<IReadOnlyCollection<WhatsAppTemplate>> ListTemplatesAsync(Guid organizationId, Guid? channelId, string? status, string? search, CancellationToken ct)
    {
        var query = db.WhatsAppTemplates.AsNoTracking().Where(x => x.OrganizationId == organizationId);
        if (channelId.HasValue) query = query.Where(x => x.WhatsAppChannelId == channelId);
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.Status == status.Trim().ToUpper());
        if (!string.IsNullOrWhiteSpace(search)) { var term = search.Trim().ToLower(); query = query.Where(x => x.Name.ToLower().Contains(term)); }
        return await query.OrderBy(x => x.Name).ThenBy(x => x.Language).ToArrayAsync(ct);
    }
    public Task<WhatsAppTemplate?> FindTemplateAsync(Guid organizationId, Guid id, CancellationToken ct) =>
        db.WhatsAppTemplates.FirstOrDefaultAsync(x => x.OrganizationId == organizationId && x.Id == id, ct);
    public async Task<IReadOnlyCollection<WhatsAppTemplate>> FindTemplatesByChannelAsync(Guid organizationId, Guid channelId, CancellationToken ct) =>
        await db.WhatsAppTemplates.Where(x => x.OrganizationId == organizationId && x.WhatsAppChannelId == channelId).ToArrayAsync(ct);
    public async Task<IReadOnlyCollection<NotificationRule>> ListRulesAsync(Guid organizationId, CancellationToken ct) =>
        await db.NotificationRules.AsNoTracking().Where(x => x.OrganizationId == organizationId).OrderBy(x => x.Name).ToArrayAsync(ct);
    public Task<NotificationRule?> FindRuleAsync(Guid organizationId, Guid id, CancellationToken ct) =>
        db.NotificationRules.FirstOrDefaultAsync(x => x.OrganizationId == organizationId && x.Id == id, ct);
    public Task<bool> RuleNameExistsAsync(Guid organizationId, string normalizedName, Guid? excludingId, CancellationToken ct) =>
        db.NotificationRules.AnyAsync(x => x.OrganizationId == organizationId && x.NormalizedName == normalizedName && x.Id != excludingId, ct);
    public async Task<IReadOnlyCollection<NotificationRuleSchedule>> ListSchedulesAsync(Guid organizationId, Guid ruleId, CancellationToken ct) =>
        await db.NotificationRuleSchedules.Where(x => x.OrganizationId == organizationId &&
            x.NotificationRuleId == ruleId && x.IsActive).ToArrayAsync(ct);
    public Task<DocumentType?> FindDocumentTypeAsync(Guid organizationId, Guid id, CancellationToken ct) =>
        db.DocumentTypes.AsNoTracking().FirstOrDefaultAsync(x => x.OrganizationId == organizationId && x.Id == id, ct);
    public async Task<IReadOnlyCollection<DocumentType>> ListDocumentTypesAsync(Guid organizationId, CancellationToken ct) =>
        await db.DocumentTypes.AsNoTracking().Where(x => x.OrganizationId == organizationId).ToArrayAsync(ct);
    public Task<DocumentCategory?> FindCategoryAsync(Guid organizationId, Guid id, CancellationToken ct) =>
        db.DocumentCategories.AsNoTracking().FirstOrDefaultAsync(x => x.OrganizationId == organizationId && x.Id == id, ct);
    public Task<Organization?> FindOrganizationAsync(Guid organizationId, CancellationToken ct) =>
        db.Organizations.FirstOrDefaultAsync(x => x.Id == organizationId, ct);
    public Task<UserAccount?> FindUserAccountAsync(Guid organizationId, Guid id, CancellationToken ct) =>
        db.UserAccounts.FirstOrDefaultAsync(x => x.OrganizationId == organizationId && x.Id == id, ct);

    public async Task<IReadOnlyCollection<NotificationEventResult>> ListEventsAsync(Guid organizationId, int limit, CancellationToken ct) =>
        await (from evt in db.NotificationEvents.AsNoTracking()
               join rule in db.NotificationRules.AsNoTracking() on evt.NotificationRuleId equals rule.Id
               join document in db.EmployeeDocuments.AsNoTracking() on evt.EmployeeDocumentId equals document.Id
               join type in db.DocumentTypes.AsNoTracking() on document.DocumentTypeId equals type.Id
               where evt.OrganizationId == organizationId
               orderby evt.OccurredAt descending
               select new NotificationEventResult(evt.Id, evt.EventCode, rule.Id, rule.Name, document.Id,
                   type.Name, evt.PayloadJson, evt.OccurredAt)).Take(limit).ToArrayAsync(ct);

    public async Task<IReadOnlyCollection<NotificationQueueResult>> ListQueueAsync(Guid organizationId, string? status, int limit, CancellationToken ct)
    {
        var query = from item in db.NotificationQueueItems.AsNoTracking()
                    join evt in db.NotificationEvents.AsNoTracking() on item.NotificationEventId equals evt.Id
                    join rule in db.NotificationRules.AsNoTracking() on evt.NotificationRuleId equals rule.Id
                    join document in db.EmployeeDocuments.AsNoTracking() on evt.EmployeeDocumentId equals document.Id
                    join type in db.DocumentTypes.AsNoTracking() on document.DocumentTypeId equals type.Id
                    where item.OrganizationId == organizationId && (status == null || item.Status == status)
                    orderby item.CreatedAt descending
                    select new { Item = item, Event = evt, RuleName = rule.Name, DocumentName = type.Name };
        var rows = await query.Take(limit).ToArrayAsync(ct);
        var ids = rows.Select(x => x.Item.Id).ToArray();
        var attempts = await db.NotificationDeliveryAttempts.AsNoTracking()
            .Where(x => ids.Contains(x.NotificationQueueItemId)).OrderBy(x => x.AttemptNumber).ToArrayAsync(ct);
        return rows.Select(row => new NotificationQueueResult(row.Item.Id, row.Event.EventCode, row.RuleName,
            row.DocumentName, row.Item.RecipientType, row.Item.Destination, row.Item.Priority, row.Item.Status,
            row.Item.DeliveryStatus, row.Item.AttemptCount, row.Item.CreatedAt, row.Item.SentAt, row.Item.LastError,
            row.Item.PayloadJson,
            attempts.Where(x => x.NotificationQueueItemId == row.Item.Id)
                .Select(x => new NotificationAttemptResult(x.AttemptNumber, x.Outcome, x.ErrorCode,
                    x.RequestJson, x.ResponseJson, x.StartedAt, x.FinishedAt)).ToArray())).ToArray();
    }

    public void AddChannel(WhatsAppChannel channel) => db.Add(channel);
    public void AddTemplate(WhatsAppTemplate template) => db.Add(template);
    public void AddRule(NotificationRule rule) => db.Add(rule);
    public void AddSchedule(NotificationRuleSchedule schedule) => db.Add(schedule);
    public async Task SaveChangesAsync(CancellationToken ct)
    {
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        { throw new NotificationException(NotificationErrorCodes.DuplicateName, "Ya existe una configuración con esos datos.", NotificationErrorKind.Conflict); }
    }
}
