using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Legaria.Application.Authentication;
using Legaria.Domain.Authentication;
using Legaria.Domain.Documents;
using Legaria.Domain.Notifications;

namespace Legaria.Application.Notifications;

public sealed partial class NotificationService(
    INotificationRepository repository,
    IWhatsAppCloudClient cloud,
    IIntegrationSecretProtector protector,
    ISecureTokenService tokens,
    IClock clock) : INotificationService
{
    private static readonly string[] EventVariables =
        ["employeeName", "documentName", "expirationDate", "daysUntilExpiration", "branchName", "organizationName"];
    private static readonly string[] RecipientCodes = ["EMPLOYEE", "BRANCH_ADMIN", "SUPER_ADMIN"];
    private static readonly string[] PriorityCodes = ["LOW", "NORMAL", "HIGH", "CRITICAL"];
    private static readonly string[] UnitCodes = ["DAY", "WEEK", "MONTH"];

    public async Task<IReadOnlyCollection<WhatsAppChannelResult>> ListChannelsAsync(CurrentAccount actor, CancellationToken ct)
    {
        var organizationId = RequireSuperAdmin(actor);
        return (await repository.ListChannelsAsync(organizationId, ct)).Select(ToChannelResult).ToArray();
    }

    public async Task<WhatsAppChannelResult> CreateChannelAsync(WhatsAppChannelInput input, CurrentAccount actor, CancellationToken ct)
    {
        var organizationId = RequireSuperAdmin(actor);
        var validated = await ValidateChannelAsync(input, organizationId, null, true, ct);
        var now = clock.UtcNow;
        var channel = WhatsAppChannel.Create(organizationId, validated.Name, validated.NormalizedName,
            validated.PhoneNumberId, validated.BusinessAccountId, protector.Protect(validated.AccessToken!),
            tokens.HashToken(validated.VerifyToken!), protector.Protect(validated.AppSecret!), now);
        repository.AddChannel(channel);
        await repository.SaveChangesAsync(ct);
        return ToChannelResult(channel);
    }

    public async Task<WhatsAppChannelResult> UpdateChannelAsync(Guid id, WhatsAppChannelInput input, CurrentAccount actor, CancellationToken ct)
    {
        var organizationId = RequireSuperAdmin(actor);
        var channel = await FindChannelAsync(organizationId, id, ct);
        var validated = await ValidateChannelAsync(input, organizationId, id, false, ct);
        channel.Update(validated.Name, validated.NormalizedName, validated.PhoneNumberId, validated.BusinessAccountId,
            validated.AccessToken is null ? null : protector.Protect(validated.AccessToken),
            validated.VerifyToken is null ? null : tokens.HashToken(validated.VerifyToken),
            validated.AppSecret is null ? null : protector.Protect(validated.AppSecret), clock.UtcNow);
        await repository.SaveChangesAsync(ct);
        return ToChannelResult(channel);
    }

    public async Task<WhatsAppChannelResult> SetChannelActiveAsync(Guid id, bool active, CurrentAccount actor, CancellationToken ct)
    {
        var organizationId = RequireSuperAdmin(actor);
        var channel = await FindChannelAsync(organizationId, id, ct);
        channel.SetActive(active, clock.UtcNow);
        await repository.SaveChangesAsync(ct);
        return ToChannelResult(channel);
    }

    public async Task<WhatsAppConnectionResult> TestChannelAsync(Guid id, CurrentAccount actor, CancellationToken ct)
    {
        var organizationId = RequireSuperAdmin(actor);
        var channel = await FindChannelAsync(organizationId, id, ct);
        var result = await cloud.TestConnectionAsync(channel.PhoneNumberId, channel.BusinessAccountId,
            protector.Unprotect(channel.EncryptedAccessToken), ct);
        if (result.Success) channel.ConnectionSucceeded(result.DisplayPhoneNumber, clock.UtcNow);
        else channel.ConnectionFailed(CleanProviderError(result.Error), clock.UtcNow);
        await repository.SaveChangesAsync(ct);
        if (result.Success && channel.Status == NotificationCodes.Active) await SynchronizeAsync(channel, ct);
        return new(result.Success, result.DisplayPhoneNumber, result.Error);
    }

    public async Task<TemplateSyncResult> SyncTemplatesAsync(Guid id, CurrentAccount actor, CancellationToken ct)
    {
        var organizationId = RequireSuperAdmin(actor);
        var channel = await FindChannelAsync(organizationId, id, ct);
        if (channel.Status != NotificationCodes.Active || channel.ConnectionStatus != NotificationCodes.Connected)
            throw new NotificationException(NotificationErrorCodes.ChannelNotConnected, "El canal debe estar activo y conectado.");
        return await SynchronizeAsync(channel, ct);
    }

    public async Task<TemplateSyncResult> SynchronizeAsync(WhatsAppChannel channel, CancellationToken ct)
    {
        var result = await cloud.GetTemplatesAsync(channel.BusinessAccountId, protector.Unprotect(channel.EncryptedAccessToken), ct);
        if (!result.Success)
        {
            channel.ConnectionFailed(CleanProviderError(result.Error), clock.UtcNow);
            await repository.SaveChangesAsync(ct);
            throw new NotificationException(NotificationErrorCodes.ProviderError, result.Error ?? "Meta no permitió sincronizar las plantillas.", NotificationErrorKind.Provider);
        }

        var now = clock.UtcNow;
        var existing = (await repository.FindTemplatesByChannelAsync(channel.OrganizationId, channel.Id, ct))
            .ToDictionary(x => x.MetaTemplateId, StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var created = 0;
        var updated = 0;
        foreach (var remote in result.Templates)
        {
            seen.Add(remote.Id);
            var variables = WhatsAppTemplateParser.DetectVariables(remote.ComponentsJson);
            var variablesJson = JsonSerializer.Serialize(variables);
            var buttonsJson = WhatsAppTemplateParser.DetectButtonsJson(remote.ComponentsJson);
            var hash = Hash($"{remote.Name}|{remote.Language}|{remote.Category}|{remote.Status}|{remote.ComponentsJson}");
            if (!existing.TryGetValue(remote.Id, out var template))
            {
                repository.AddTemplate(WhatsAppTemplate.Create(channel.OrganizationId, channel.Id, remote.Id,
                    remote.Name, remote.Category, remote.Language, remote.Status, remote.ComponentsJson,
                    variablesJson, buttonsJson, hash, now));
                created++;
            }
            else
            {
                template.Synchronize(remote.Name, remote.Category, remote.Language, remote.Status,
                    remote.ComponentsJson, variablesJson, buttonsJson, hash, now);
                updated++;
            }
        }
        var unavailable = 0;
        foreach (var template in existing.Values.Where(x => x.IsAvailable && !seen.Contains(x.MetaTemplateId)))
        {
            template.MarkUnavailable(now);
            unavailable++;
        }
        channel.Synchronized(now);
        await repository.SaveChangesAsync(ct);
        return new(created, updated, unavailable, now);
    }

    public async Task<IReadOnlyCollection<WhatsAppTemplateResult>> ListTemplatesAsync(Guid? channelId, string? status, string? search, CurrentAccount actor, CancellationToken ct)
    {
        var organizationId = RequireSuperAdmin(actor);
        return (await repository.ListTemplatesAsync(organizationId, channelId, status, search, ct)).Select(ToTemplateResult).ToArray();
    }

    public async Task<IReadOnlyCollection<NotificationRuleResult>> ListRulesAsync(CurrentAccount actor, CancellationToken ct)
    {
        var organizationId = RequireSuperAdmin(actor);
        return await BuildRuleResultsAsync(organizationId, await repository.ListRulesAsync(organizationId, ct), ct);
    }

    public async Task<NotificationRuleResult> CreateRuleAsync(NotificationRuleInput input, CurrentAccount actor, CancellationToken ct)
    {
        var organizationId = RequireSuperAdmin(actor);
        var validated = await ValidateRuleAsync(input, organizationId, null, ct);
        var now = clock.UtcNow;
        var rule = NotificationRule.Create(organizationId, validated.Name, validated.NormalizedName,
            input.DocumentTypeId, input.WhatsAppChannelId, input.WhatsAppTemplateId, validated.Priority,
            JsonSerializer.Serialize(validated.Recipients), JsonSerializer.Serialize(input.VariableMappings),
            validated.Template.ContentHash, now);
        repository.AddRule(rule);
        foreach (var schedule in validated.Schedules)
            repository.AddSchedule(NotificationRuleSchedule.Create(organizationId, rule.Id, schedule.Amount, schedule.Unit));
        await repository.SaveChangesAsync(ct);
        return (await BuildRuleResultsAsync(organizationId, [rule], ct)).Single();
    }

    public async Task<NotificationRuleResult> UpdateRuleAsync(Guid id, NotificationRuleInput input, CurrentAccount actor, CancellationToken ct)
    {
        var organizationId = RequireSuperAdmin(actor);
        var rule = await repository.FindRuleAsync(organizationId, id, ct)
            ?? throw NotFound();
        var validated = await ValidateRuleAsync(input, organizationId, id, ct);
        rule.Update(validated.Name, validated.NormalizedName, input.DocumentTypeId, input.WhatsAppChannelId,
            input.WhatsAppTemplateId, validated.Priority, JsonSerializer.Serialize(validated.Recipients),
            JsonSerializer.Serialize(input.VariableMappings), validated.Template.ContentHash, clock.UtcNow);
        foreach (var schedule in await repository.ListSchedulesAsync(organizationId, id, ct)) schedule.Deactivate();
        foreach (var schedule in validated.Schedules)
            repository.AddSchedule(NotificationRuleSchedule.Create(organizationId, rule.Id, schedule.Amount, schedule.Unit));
        await repository.SaveChangesAsync(ct);
        return (await BuildRuleResultsAsync(organizationId, [rule], ct)).Single();
    }

    public async Task<NotificationRuleResult> SetRuleActiveAsync(Guid id, bool active, CurrentAccount actor, CancellationToken ct)
    {
        var organizationId = RequireSuperAdmin(actor);
        var rule = await repository.FindRuleAsync(organizationId, id, ct) ?? throw NotFound();
        if (active)
        {
            var template = await repository.FindTemplateAsync(organizationId, rule.WhatsAppTemplateId, ct);
            var channel = await repository.FindChannelAsync(organizationId, rule.WhatsAppChannelId, ct);
            if (template is null || !template.IsAvailable || template.Status != "APPROVED" || template.ContentHash != rule.TemplateContentHash)
                throw new NotificationException(NotificationErrorCodes.TemplateUnavailable, "La plantilla cambió o ya no está aprobada.");
            if (channel?.Status != NotificationCodes.Active || channel.ConnectionStatus != NotificationCodes.Connected)
                throw new NotificationException(NotificationErrorCodes.ChannelNotConnected, "El canal debe estar activo y conectado.");
        }
        rule.SetActive(active, clock.UtcNow);
        await repository.SaveChangesAsync(ct);
        return (await BuildRuleResultsAsync(organizationId, [rule], ct)).Single();
    }

    public Task<IReadOnlyCollection<NotificationEventResult>> ListEventsAsync(int limit, CurrentAccount actor, CancellationToken ct)
    {
        var organizationId = RequireSuperAdmin(actor);
        if (limit is < 1 or > 200) throw Invalid("El límite debe estar entre 1 y 200.");
        return repository.ListEventsAsync(organizationId, limit, ct);
    }

    public Task<IReadOnlyCollection<NotificationQueueResult>> ListQueueAsync(string? status, int limit, CurrentAccount actor, CancellationToken ct)
    {
        var organizationId = RequireSuperAdmin(actor);
        if (limit is < 1 or > 200) throw Invalid("El límite debe estar entre 1 y 200.");
        var normalizedStatus = string.IsNullOrWhiteSpace(status) ? null : status.Trim().ToUpperInvariant();
        if (normalizedStatus is not null && normalizedStatus is not (NotificationCodes.Pending or NotificationCodes.Processing or NotificationCodes.Sent or NotificationCodes.Failed or NotificationCodes.Cancelled))
            throw Invalid("El estado de cola no es válido.");
        return repository.ListQueueAsync(organizationId, normalizedStatus, limit, ct);
    }

    public async Task<NotificationContactResult> GetContactAsync(CurrentAccount actor, CancellationToken ct)
    {
        var organizationId = RequireTenant(actor);
        var account = await repository.FindUserAccountAsync(organizationId, actor.UserId, ct) ?? throw NotFound();
        return new(account.MobilePhone, account.WhatsAppConsentAt);
    }

    public async Task<NotificationContactResult> UpdateContactAsync(NotificationContactInput input, CurrentAccount actor, CancellationToken ct)
    {
        var organizationId = RequireTenant(actor);
        var account = await repository.FindUserAccountAsync(organizationId, actor.UserId, ct) ?? throw NotFound();
        var phone = NormalizePhone(input.MobilePhone);
        if (input.WhatsAppConsent && phone is null) throw Invalid("Debes registrar un teléfono antes de autorizar WhatsApp.");
        account.UpdateNotificationContact(phone, input.WhatsAppConsent, clock.UtcNow);
        await repository.SaveChangesAsync(ct);
        return new(account.MobilePhone, account.WhatsAppConsentAt);
    }

    public async Task<NotificationSettingsResult> GetSettingsAsync(CurrentAccount actor, CancellationToken ct)
    {
        var organizationId = RequireSuperAdmin(actor);
        var organization = await repository.FindOrganizationAsync(organizationId, ct) ?? throw NotFound();
        return new(organization.TimeZoneId, organization.NotificationTime);
    }

    public async Task<NotificationSettingsResult> UpdateSettingsAsync(NotificationSettingsInput input, CurrentAccount actor, CancellationToken ct)
    {
        var organizationId = RequireSuperAdmin(actor);
        if (!TryFindTimeZone(input.TimeZoneId.Trim(), out _)) throw Invalid("La zona horaria no es válida.");
        var organization = await repository.FindOrganizationAsync(organizationId, ct) ?? throw NotFound();
        organization.UpdateNotificationSettings(input.TimeZoneId.Trim(), input.NotificationTime, clock.UtcNow);
        await repository.SaveChangesAsync(ct);
        return new(organization.TimeZoneId, organization.NotificationTime);
    }

    private async Task<ChannelValidation> ValidateChannelAsync(WhatsAppChannelInput input, Guid organizationId, Guid? excludingId, bool requireSecrets, CancellationToken ct)
    {
        var name = CleanName(input.Name, 150);
        var normalizedName = name.ToUpperInvariant();
        var phoneId = Required(input.PhoneNumberId, 64, "Phone Number ID");
        var businessId = Required(input.BusinessAccountId, 64, "Business Account ID");
        var accessToken = CleanOptionalSecret(input.AccessToken);
        var verifyToken = CleanOptionalSecret(input.WebhookVerifyToken);
        var appSecret = CleanOptionalSecret(input.AppSecret);
        if (requireSecrets && (accessToken is null || verifyToken is null || appSecret is null))
            throw Invalid("Access Token, Webhook Verify Token y App Secret son obligatorios.");
        if (await repository.ChannelNameExistsAsync(organizationId, normalizedName, excludingId, ct))
            throw new NotificationException(NotificationErrorCodes.DuplicateName, "Ya existe un canal con ese nombre.", NotificationErrorKind.Conflict);
        if (await repository.PhoneNumberExistsAsync(phoneId, excludingId, ct))
            throw new NotificationException(NotificationErrorCodes.InvalidData, "El Phone Number ID ya está configurado.", NotificationErrorKind.Conflict);
        if (verifyToken is not null && await repository.VerifyHashExistsAsync(tokens.HashToken(verifyToken), excludingId, ct))
            throw new NotificationException(NotificationErrorCodes.InvalidData, "El Webhook Verify Token ya está configurado.", NotificationErrorKind.Conflict);
        return new(name, normalizedName, phoneId, businessId, accessToken, verifyToken, appSecret);
    }

    private async Task<RuleValidation> ValidateRuleAsync(NotificationRuleInput input, Guid organizationId, Guid? excludingId, CancellationToken ct)
    {
        var name = CleanName(input.Name, 150);
        var normalized = name.ToUpperInvariant();
        if (await repository.RuleNameExistsAsync(organizationId, normalized, excludingId, ct))
            throw new NotificationException(NotificationErrorCodes.DuplicateName, "Ya existe una alerta con ese nombre.", NotificationErrorKind.Conflict);
        var priority = input.Priority.Trim().ToUpperInvariant();
        if (!PriorityCodes.Contains(priority)) throw Invalid("La prioridad no es válida.");
        var recipients = input.Recipients.Select(x => x.Trim().ToUpperInvariant()).Distinct().ToArray();
        if (recipients.Length == 0 || recipients.Any(x => !RecipientCodes.Contains(x))) throw Invalid("Selecciona al menos un destinatario válido.");
        var schedules = input.Schedules.Select(x => new NotificationScheduleInput(x.Amount, x.Unit.Trim().ToUpperInvariant())).ToArray();
        if (schedules.Length is < 1 or > 3 || schedules.Any(x => x.Amount is <= 0 or > 3650 || !UnitCodes.Contains(x.Unit)) || schedules.DistinctBy(x => (x.Amount, x.Unit)).Count() != schedules.Length)
            throw Invalid("Configura entre una y tres anticipaciones válidas y diferentes.");
        var type = await repository.FindDocumentTypeAsync(organizationId, input.DocumentTypeId, ct) ?? throw NotFound();
        var category = await repository.FindCategoryAsync(organizationId, type.CategoryId, ct) ?? throw NotFound();
        if (type.Status != DocumentCatalogStatus.Active || category.Status != DocumentCatalogStatus.Active || category.Scope != DocumentScope.Employee || type.ExpirationDateMode == DocumentDateMode.Never)
            throw Invalid("El tipo debe ser un documento activo de trabajador que permita vencimiento.");
        var channel = await repository.FindChannelAsync(organizationId, input.WhatsAppChannelId, ct) ?? throw NotFound();
        var template = await repository.FindTemplateAsync(organizationId, input.WhatsAppTemplateId, ct) ?? throw NotFound();
        if (template.WhatsAppChannelId != channel.Id || !template.IsAvailable || template.Status != "APPROVED")
            throw new NotificationException(NotificationErrorCodes.TemplateUnavailable, "Selecciona una plantilla aprobada y disponible del canal.");
        var templateVariables = WhatsAppTemplateParser.DetectVariables(template.ComponentsJson).ToHashSet(StringComparer.Ordinal);
        if (!templateVariables.SetEquals(input.VariableMappings.Keys) || input.VariableMappings.Values.Any(x => !EventVariables.Contains(x)))
            throw Invalid("Todas las variables de la plantilla deben mapearse a variables disponibles del evento.");
        return new(name, normalized, priority, recipients, schedules, template);
    }

    private async Task<IReadOnlyCollection<NotificationRuleResult>> BuildRuleResultsAsync(Guid organizationId, IReadOnlyCollection<NotificationRule> rules, CancellationToken ct)
    {
        var channels = (await repository.ListChannelsAsync(organizationId, ct)).ToDictionary(x => x.Id);
        var templates = (await repository.ListTemplatesAsync(organizationId, null, null, null, ct)).ToDictionary(x => x.Id);
        var types = (await repository.ListDocumentTypesAsync(organizationId, ct)).ToDictionary(x => x.Id);
        var output = new List<NotificationRuleResult>();
        foreach (var rule in rules)
        {
            var template = templates[rule.WhatsAppTemplateId];
            var channel = channels[rule.WhatsAppChannelId];
            var blocked = !template.IsAvailable || template.Status != "APPROVED" || template.ContentHash != rule.TemplateContentHash || channel.Status != NotificationCodes.Active || channel.ConnectionStatus != NotificationCodes.Connected;
            output.Add(new(rule.Id, rule.Name, rule.EventCode, rule.DocumentTypeId, types[rule.DocumentTypeId].Name,
                rule.WhatsAppChannelId, channel.Name, rule.WhatsAppTemplateId, template.Name, rule.Priority,
                rule.Status, blocked, blocked ? "El canal o la plantilla requiere atención." : null,
                JsonSerializer.Deserialize<string[]>(rule.RecipientsJson) ?? [],
                JsonSerializer.Deserialize<Dictionary<string, string>>(rule.VariableMappingsJson) ?? [],
                (await repository.ListSchedulesAsync(organizationId, rule.Id, ct)).Select(x => new NotificationScheduleResult(x.Id, x.Amount, x.Unit)).ToArray()));
        }
        return output;
    }

    private async Task<WhatsAppChannel> FindChannelAsync(Guid organizationId, Guid id, CancellationToken ct) =>
        await repository.FindChannelAsync(organizationId, id, ct) ?? throw NotFound();
    private static Guid RequireSuperAdmin(CurrentAccount actor)
    {
        var organizationId = RequireTenant(actor);
        if (!actor.Roles.Contains(SystemRoleCodes.SuperAdmin)) throw new NotificationException(NotificationErrorCodes.Forbidden, "No tienes permiso para administrar notificaciones.", NotificationErrorKind.Forbidden);
        return organizationId;
    }
    private static Guid RequireTenant(CurrentAccount actor) => actor.AccountType == AccountType.Tenant && actor.OrganizationId.HasValue
        ? actor.OrganizationId.Value : throw new NotificationException(NotificationErrorCodes.Forbidden, "Se requiere una cuenta de organización.", NotificationErrorKind.Forbidden);
    private static string CleanName(string value, int max) { var clean = SpaceRegex().Replace(value?.Trim() ?? "", " "); if (clean.Length is < 2 || clean.Length > max) throw Invalid("El nombre no es válido."); return clean; }
    private static string Required(string? value, int max, string label) { var clean = value?.Trim() ?? ""; if (clean.Length == 0 || clean.Length > max) throw Invalid($"{label} no es válido."); return clean; }
    private static string? CleanOptionalSecret(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static string? NormalizePhone(string? value) { if (string.IsNullOrWhiteSpace(value)) return null; var clean = value.Trim().Replace(" ", ""); if (!PhoneRegex().IsMatch(clean)) throw Invalid("El teléfono debe usar formato internacional, por ejemplo +573001234567."); return clean; }
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private static string CleanProviderError(string? error) => string.IsNullOrWhiteSpace(error) ? "Error de conexión con Meta." : error.Length > 1000 ? error[..1000] : error;
    private static bool TryFindTimeZone(string id, out TimeZoneInfo? zone) { try { zone = TimeZoneInfo.FindSystemTimeZoneById(id); return true; } catch (TimeZoneNotFoundException) { zone = null; return false; } catch (InvalidTimeZoneException) { zone = null; return false; } }
    private static NotificationException Invalid(string message) => new(NotificationErrorCodes.InvalidData, message);
    private static NotificationException NotFound() => new(NotificationErrorCodes.NotFound, "El recurso no existe.", NotificationErrorKind.NotFound);
    private static WhatsAppChannelResult ToChannelResult(WhatsAppChannel x) => new(x.Id, x.Name, x.PhoneNumberId, x.BusinessAccountId, x.DisplayPhoneNumber, x.Status, x.ConnectionStatus, true, true, true, x.LastVerifiedAt, x.LastSynchronizedAt, x.LastError);
    private static WhatsAppTemplateResult ToTemplateResult(WhatsAppTemplate x) => new(x.Id, x.WhatsAppChannelId, x.Name, x.Category, x.Language, x.Status, x.ComponentsJson, JsonSerializer.Deserialize<string[]>(x.VariablesJson) ?? [], x.ButtonsJson, x.IsAvailable, x.LastSynchronizedAt, x.LastChangedAt);
    private sealed record ChannelValidation(string Name, string NormalizedName, string PhoneNumberId, string BusinessAccountId, string? AccessToken, string? VerifyToken, string? AppSecret);
    private sealed record RuleValidation(string Name, string NormalizedName, string Priority, string[] Recipients, NotificationScheduleInput[] Schedules, WhatsAppTemplate Template);
    [GeneratedRegex(@"\s+")] private static partial Regex SpaceRegex();
    [GeneratedRegex(@"^\+[1-9]\d{7,14}$")] private static partial Regex PhoneRegex();
}
