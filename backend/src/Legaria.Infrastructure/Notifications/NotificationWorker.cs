using System.Text.Json;
using Legaria.Application.Notifications;
using Legaria.Application.Authentication;
using Legaria.Domain.Authentication;
using Legaria.Domain.Notifications;
using Legaria.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Legaria.Infrastructure.Notifications;

public sealed class NotificationWorker(IServiceScopeFactory scopeFactory, ILogger<NotificationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var processor = scope.ServiceProvider.GetRequiredService<NotificationProcessor>();
                await processor.RunAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception exception) { logger.LogError(exception, "Falló el ciclo de notificaciones."); }
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }
}

public sealed class NotificationProcessor(
    LegariaDbContext db,
    NotificationService notificationService,
    IWhatsAppCloudClient cloud,
    IIntegrationSecretProtector protector,
    IClock clock,
    ILogger<NotificationProcessor> logger)
{
    private readonly string _workerId = $"{Environment.MachineName}:{Guid.NewGuid():N}";

    public async Task RunAsync(CancellationToken ct)
    {
        await RecoverLeasesAsync(ct);
        await CancelInvalidPendingAsync(ct);
        await SynchronizeChannelsAsync(ct);
        await EvaluateExpirationsAsync(ct);
        for (var i = 0; i < 20; i++)
        {
            var item = await ClaimAsync(ct);
            if (item is null) break;
            await SendAsync(item.Id, ct);
        }
    }

    public async Task EvaluateExpirationsAsync(CancellationToken ct)
    {
        var now = clock.UtcNow;
        var rules = await db.NotificationRules.AsNoTracking()
            .Where(x => x.Status == NotificationCodes.Active)
            .ToArrayAsync(ct);
        foreach (var rule in rules)
        {
            var organization = await db.Organizations.AsNoTracking().FirstAsync(x => x.Id == rule.OrganizationId, ct);
            var localNow = TimeZoneInfo.ConvertTime(now, FindTimeZone(organization.TimeZoneId));
            if (TimeOnly.FromDateTime(localNow.DateTime) < organization.NotificationTime && now - rule.UpdatedAt > TimeSpan.FromMinutes(2)) continue;
            var today = DateOnly.FromDateTime(localNow.DateTime);
            var schedules = await db.NotificationRuleSchedules.AsNoTracking()
                .Where(x => x.OrganizationId == rule.OrganizationId && x.NotificationRuleId == rule.Id && x.IsActive).ToArrayAsync(ct);
            var documents = await (from document in db.EmployeeDocuments.AsNoTracking()
                                   join employee in db.Employees.AsNoTracking() on new { document.OrganizationId, Id = document.EmployeeId } equals new { employee.OrganizationId, employee.Id }
                                   join relationship in db.EmploymentRelationships.AsNoTracking() on new { employee.OrganizationId, EmployeeId = employee.Id } equals new { relationship.OrganizationId, relationship.EmployeeId }
                                   join type in db.DocumentTypes.AsNoTracking() on new { document.OrganizationId, Id = document.DocumentTypeId } equals new { type.OrganizationId, type.Id }
                                   where document.OrganizationId == rule.OrganizationId && document.DocumentTypeId == rule.DocumentTypeId
                                       && document.ReplacedAt == null && document.ExpiresOn != null && document.ExpiresOn >= today
                                       && relationship.EndedOn == null
                                   select new { Document = document, Employee = employee, Relationship = relationship, TypeName = type.Name }).ToArrayAsync(ct);
            foreach (var row in documents)
            {
                var due = schedules.Select(schedule => new { Schedule = schedule, Date = schedule.ScheduledOn(row.Document.ExpiresOn!.Value) })
                    .Where(x => x.Date <= today).OrderByDescending(x => x.Date).ToArray();
                if (due.Length == 0) continue;
                var keys = due.Select(x => OccurrenceKey(rule.Id, row.Document.Id, x.Schedule.Id)).ToArray();
                var existing = (await db.NotificationEvents.AsNoTracking().Where(x => keys.Contains(x.OccurrenceKey)).Select(x => x.OccurrenceKey).ToArrayAsync(ct)).ToHashSet();
                var selected = due.FirstOrDefault(x => !existing.Contains(OccurrenceKey(rule.Id, row.Document.Id, x.Schedule.Id)));
                if (selected is null) continue;
                foreach (var dueSchedule in due.Where(x => !existing.Contains(OccurrenceKey(rule.Id, row.Document.Id, x.Schedule.Id))))
                {
                    var isSelected = dueSchedule.Schedule.Id == selected.Schedule.Id;
                    await CreateEventAsync(rule, row.Document.Id, row.Employee.Id, row.Relationship.Id, row.TypeName,
                        row.Document.ExpiresOn.GetValueOrDefault(), dueSchedule.Schedule, organization.TradeName, today, isSelected, now, ct);
                }
                await db.SaveChangesAsync(ct);
            }
        }
    }

    private async Task CreateEventAsync(NotificationRule rule, Guid documentId, Guid employeeId, Guid relationshipId,
        string documentName, DateOnly expirationDate, NotificationRuleSchedule schedule, string organizationName,
        DateOnly today, bool enqueue, DateTimeOffset now, CancellationToken ct)
    {
        var employee = await db.Employees.AsNoTracking().FirstAsync(x => x.OrganizationId == rule.OrganizationId && x.Id == employeeId, ct);
        var assignment = await (from item in db.EmployeeAssignments.AsNoTracking()
                                join branch in db.Branches.AsNoTracking() on new { item.OrganizationId, Id = item.BranchId } equals new { branch.OrganizationId, branch.Id }
                                where item.OrganizationId == rule.OrganizationId && item.EmploymentRelationshipId == relationshipId && item.EndedOn == null && item.IsPrimary
                                select new { item.BranchId, branch.Name }).FirstOrDefaultAsync(ct);
        var values = new Dictionary<string, string>
        {
            ["employeeName"] = $"{employee.FirstName} {employee.LastName}".Trim(),
            ["documentName"] = documentName,
            ["expirationDate"] = expirationDate.ToString("yyyy-MM-dd"),
            ["daysUntilExpiration"] = (expirationDate.DayNumber - today.DayNumber).ToString(),
            ["branchName"] = assignment?.Name ?? string.Empty,
            ["organizationName"] = organizationName
        };
        var payload = JsonSerializer.Serialize(values);
        var evt = NotificationEvent.Create(rule.OrganizationId, rule.Id, documentId, schedule.Id,
            OccurrenceKey(rule.Id, documentId, schedule.Id), payload, now);
        db.Add(evt);
        if (!enqueue) return;

        var recipients = JsonSerializer.Deserialize<string[]>(rule.RecipientsJson) ?? [];
        var destinations = new List<ResolvedRecipient>();
        if (recipients.Contains("EMPLOYEE"))
            destinations.Add(new("EMPLOYEE", employee.Id, employee.MobilePhone,
                employee.MobilePhone is null ? "El trabajador no tiene teléfono." : employee.WhatsAppConsentAt is null ? "El trabajador no autorizó WhatsApp." : null));
        if (recipients.Contains("BRANCH_ADMIN"))
        {
            if (assignment is null) destinations.Add(new("BRANCH_ADMIN", null, null, "El trabajador no tiene asignación principal activa."));
            else
            {
                var admins = await ActiveAccountsByRoleAsync(rule.OrganizationId, SystemRoleCodes.BranchAdmin, assignment.BranchId, ct);
                if (admins.Count == 0) destinations.Add(new("BRANCH_ADMIN", null, null, "La sucursal no tiene administradores activos."));
                destinations.AddRange(admins.Select(x => ResolveAccount("BRANCH_ADMIN", x)));
            }
        }
        if (recipients.Contains("SUPER_ADMIN"))
        {
            var admins = await ActiveAccountsByRoleAsync(rule.OrganizationId, SystemRoleCodes.SuperAdmin, null, ct);
            if (admins.Count == 0) destinations.Add(new("SUPER_ADMIN", null, null, "La organización no tiene superadministradores activos."));
            destinations.AddRange(admins.Select(x => ResolveAccount("SUPER_ADMIN", x)));
        }

        var usedPhones = new HashSet<string>(StringComparer.Ordinal);
        foreach (var recipient in destinations)
        {
            var duplicate = recipient.Destination is not null && !usedPhones.Add(recipient.Destination);
            if (duplicate) continue;
            var reason = recipient.Reason;
            var identity = recipient.RecipientId?.ToString("N") ?? Guid.NewGuid().ToString("N");
            db.Add(NotificationQueueItem.Create(rule.OrganizationId, evt.Id, rule.WhatsAppChannelId,
                recipient.Type, recipient.RecipientId, recipient.Destination,
                $"{evt.OccurrenceKey}:{recipient.Destination ?? identity}", rule.Priority, payload, now, reason));
        }
    }

    private async Task<IReadOnlyCollection<UserAccount>> ActiveAccountsByRoleAsync(Guid organizationId, string roleCode, Guid? branchId, CancellationToken ct)
    {
        var query = from account in db.UserAccounts.AsNoTracking()
                    join userRole in db.UserRoles.AsNoTracking() on account.Id equals userRole.UserAccountId
                    join role in db.SystemRoles.AsNoTracking() on userRole.SystemRoleId equals role.Id
                    where account.OrganizationId == organizationId && account.Status == AccountStatus.Active && role.Code == roleCode
                    select account;
        if (branchId.HasValue)
            query = from account in query
                    join access in db.UserBranchAccesses.AsNoTracking() on account.Id equals access.UserAccountId
                    where access.OrganizationId == organizationId && access.BranchId == branchId && access.RevokedAt == null
                    select account;
        return await query.Distinct().ToArrayAsync(ct);
    }

    private async Task SynchronizeChannelsAsync(CancellationToken ct)
    {
        var cutoff = clock.UtcNow.AddHours(-6);
        var ids = await db.WhatsAppChannels.AsNoTracking()
            .Where(x => x.Status == NotificationCodes.Active && x.ConnectionStatus == NotificationCodes.Connected
                && (x.LastSynchronizedAt == null || x.LastSynchronizedAt < cutoff)).Select(x => x.Id).ToArrayAsync(ct);
        foreach (var id in ids)
        {
            try
            {
                var channel = await db.WhatsAppChannels.FirstAsync(x => x.Id == id, ct);
                await notificationService.SynchronizeAsync(channel, ct);
            }
            catch (NotificationException exception) { logger.LogWarning("No se sincronizó el canal {ChannelId}: {Code}", id, exception.Code); }
        }
    }

    private async Task RecoverLeasesAsync(CancellationToken ct)
    {
        var cutoff = clock.UtcNow.AddMinutes(-10);
        await db.NotificationQueueItems.Where(x => x.Status == NotificationCodes.Processing && x.LockedAt < cutoff)
            .ExecuteUpdateAsync(update => update.SetProperty(x => x.Status, NotificationCodes.Pending)
                .SetProperty(x => x.LockedAt, (DateTimeOffset?)null).SetProperty(x => x.WorkerId, (string?)null), ct);
    }

    private async Task CancelInvalidPendingAsync(CancellationToken ct)
    {
        var rows = await (from item in db.NotificationQueueItems
                          join evt in db.NotificationEvents on item.NotificationEventId equals evt.Id
                          join rule in db.NotificationRules on evt.NotificationRuleId equals rule.Id
                          join document in db.EmployeeDocuments on evt.EmployeeDocumentId equals document.Id
                          join template in db.WhatsAppTemplates on rule.WhatsAppTemplateId equals template.Id
                          join channel in db.WhatsAppChannels on rule.WhatsAppChannelId equals channel.Id
                          where item.Status == NotificationCodes.Pending
                          select new { Item = item, Rule = rule, Document = document, Template = template, Channel = channel }).ToArrayAsync(ct);
        var changed = false;
        foreach (var row in rows)
        {
            var reason = await InvalidReasonAsync(row.Item, row.Rule, row.Document, row.Template, row.Channel, ct);
            if (reason is null) continue;
            row.Item.Cancel(reason, clock.UtcNow);
            changed = true;
        }
        if (changed) await db.SaveChangesAsync(ct);
    }

    private async Task<NotificationQueueItem?> ClaimAsync(CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var rows = await db.NotificationQueueItems
            .FromSqlInterpolated($"SELECT * FROM notification_queue WHERE status = 'PENDING' AND next_attempt_at <= {clock.UtcNow} ORDER BY CASE priority WHEN 'CRITICAL' THEN 0 WHEN 'HIGH' THEN 1 WHEN 'NORMAL' THEN 2 ELSE 3 END, next_attempt_at FOR UPDATE SKIP LOCKED LIMIT 1")
            .ToListAsync(ct);
        var claimed = rows.SingleOrDefault();
        if (claimed is null) { await transaction.CommitAsync(ct); return null; }
        claimed.Claim(_workerId, clock.UtcNow);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return claimed;
    }

    private async Task SendAsync(Guid itemId, CancellationToken ct)
    {
        db.ChangeTracker.Clear();
        var row = await (from item in db.NotificationQueueItems
                         join evt in db.NotificationEvents on item.NotificationEventId equals evt.Id
                         join rule in db.NotificationRules on evt.NotificationRuleId equals rule.Id
                         join document in db.EmployeeDocuments on evt.EmployeeDocumentId equals document.Id
                         join template in db.WhatsAppTemplates on rule.WhatsAppTemplateId equals template.Id
                         join channel in db.WhatsAppChannels on rule.WhatsAppChannelId equals channel.Id
                         where item.Id == itemId
                         select new { Item = item, Event = evt, Rule = rule, Document = document, Template = template, Channel = channel }).SingleAsync(ct);
        var invalid = await InvalidReasonAsync(row.Item, row.Rule, row.Document, row.Template, row.Channel, ct);
        if (invalid is not null) { row.Item.Cancel(invalid, clock.UtcNow); await db.SaveChangesAsync(ct); return; }
        var mappings = JsonSerializer.Deserialize<Dictionary<string, string>>(row.Rule.VariableMappingsJson) ?? [];
        var values = JsonSerializer.Deserialize<Dictionary<string, string>>(row.Event.PayloadJson) ?? [];
        var started = clock.UtcNow;
        var result = await cloud.SendTemplateAsync(row.Channel.PhoneNumberId, protector.Unprotect(row.Channel.EncryptedAccessToken),
            row.Item.Destination!, row.Template.Name, row.Template.Language, row.Template.ComponentsJson, mappings, values, ct);
        var finished = clock.UtcNow;
        var attemptNumber = row.Item.AttemptCount + 1;
        db.Add(NotificationDeliveryAttempt.Create(row.Item.OrganizationId, row.Item.Id, attemptNumber,
            result.RequestJson,
            result.ResponseJson, result.Success ? "SENT" : "FAILED", result.ErrorCode, started, finished));
        if (result.Success)
        {
            row.Item.RecordAttempt();
            row.Item.Sent(result.MessageId ?? string.Empty, finished);
        }
        else if (result.Transient && attemptNumber < 5)
        {
            var delay = result.RetryAfter ?? TimeSpan.FromMinutes(5 * Math.Pow(2, attemptNumber - 1));
            row.Item.Retry(Limit(result.Error), finished.Add(delay), finished);
        }
        else row.Item.Fail(Limit(result.Error), finished);
        await db.SaveChangesAsync(ct);
    }

    private async Task<string?> InvalidReasonAsync(NotificationQueueItem item, NotificationRule rule,
        Domain.Documents.EmployeeDocument document, WhatsAppTemplate template, WhatsAppChannel channel, CancellationToken ct)
    {
        if (rule.Status != NotificationCodes.Active) return "La alerta fue desactivada.";
        if (document.ReplacedAt is not null) return "La versión documental fue reemplazada.";
        if (!await db.EmploymentRelationships.AnyAsync(x => x.OrganizationId == item.OrganizationId && x.EmployeeId == document.EmployeeId && x.EndedOn == null, ct)) return "La relación laboral finalizó.";
        if (channel.Status != NotificationCodes.Active || channel.ConnectionStatus != NotificationCodes.Connected) return "El canal no está disponible.";
        if (!template.IsAvailable || template.Status != "APPROVED" || template.ContentHash != rule.TemplateContentHash) return "La plantilla cambió o dejó de estar aprobada.";
        if (item.Destination is null) return item.LastError ?? "No existe un destino.";
        if (item.RecipientType == "EMPLOYEE")
        {
            var employee = await db.Employees.AsNoTracking().FirstOrDefaultAsync(x => x.OrganizationId == item.OrganizationId && x.Id == item.RecipientId, ct);
            if (employee?.WhatsAppConsentAt is null || employee.MobilePhone != item.Destination) return "El trabajador revocó el consentimiento o cambió su contacto.";
        }
        else
        {
            var account = await db.UserAccounts.AsNoTracking().FirstOrDefaultAsync(x => x.OrganizationId == item.OrganizationId && x.Id == item.RecipientId, ct);
            if (account?.Status != AccountStatus.Active || account.WhatsAppConsentAt is null || account.MobilePhone != item.Destination) return "La cuenta no está activa o revocó el consentimiento.";
        }
        return null;
    }

    private static string OccurrenceKey(Guid ruleId, Guid documentId, Guid scheduleId) => $"{ruleId:N}:{documentId:N}:{scheduleId:N}";
    private static TimeZoneInfo FindTimeZone(string id) => TimeZoneInfo.FindSystemTimeZoneById(id);
    private static ResolvedRecipient ResolveAccount(string type, UserAccount account) => new(type, account.Id, account.MobilePhone,
        account.MobilePhone is null ? "La cuenta no tiene teléfono." : account.WhatsAppConsentAt is null ? "La cuenta no autorizó WhatsApp." : null);
    private static string Limit(string? value) => string.IsNullOrWhiteSpace(value) ? "Error del proveedor." : value.Length > 1000 ? value[..1000] : value;
    private sealed record ResolvedRecipient(string Type, Guid? RecipientId, string? Destination, string? Reason);
}
