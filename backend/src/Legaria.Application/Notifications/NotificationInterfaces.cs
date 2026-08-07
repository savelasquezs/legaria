using Legaria.Application.Authentication;
using Legaria.Domain.Authentication;
using Legaria.Domain.Documents;
using Legaria.Domain.Notifications;
using Legaria.Domain.Tenancy;

namespace Legaria.Application.Notifications;

public sealed record MetaConnectionResult(bool Success, string? DisplayPhoneNumber, string? Error);
public sealed record MetaTemplate(string Id, string Name, string Language, string Category, string Status, string ComponentsJson);
public sealed record MetaTemplateSyncResult(bool Success, IReadOnlyCollection<MetaTemplate> Templates, string? Error);
public sealed record MetaTemplateSendResult(bool Success, bool Transient, string? MessageId, string RequestJson, string? ResponseJson,
    string? ErrorCode, string? Error, TimeSpan? RetryAfter);

public interface IWhatsAppCloudClient
{
    Task<MetaConnectionResult> TestConnectionAsync(string phoneNumberId, string businessAccountId,
        string accessToken, CancellationToken cancellationToken);
    Task<MetaTemplateSyncResult> GetTemplatesAsync(string businessAccountId, string accessToken,
        CancellationToken cancellationToken);
    Task<MetaTemplateSendResult> SendTemplateAsync(string phoneNumberId, string accessToken, string destination,
        string templateName, string language, string componentsJson, IReadOnlyDictionary<string, string> mappings,
        IReadOnlyDictionary<string, string> values, CancellationToken cancellationToken);
}

public interface IIntegrationSecretProtector
{
    string Protect(string value);
    string Unprotect(string value);
}

public interface INotificationRepository
{
    Task<IReadOnlyCollection<WhatsAppChannel>> ListChannelsAsync(Guid organizationId, CancellationToken ct);
    Task<WhatsAppChannel?> FindChannelAsync(Guid organizationId, Guid id, CancellationToken ct);
    Task<bool> ChannelNameExistsAsync(Guid organizationId, string normalizedName, Guid? excludingId, CancellationToken ct);
    Task<bool> PhoneNumberExistsAsync(string phoneNumberId, Guid? excludingId, CancellationToken ct);
    Task<bool> VerifyHashExistsAsync(string hash, Guid? excludingId, CancellationToken ct);
    Task<IReadOnlyCollection<WhatsAppTemplate>> ListTemplatesAsync(Guid organizationId, Guid? channelId, string? status, string? search, CancellationToken ct);
    Task<WhatsAppTemplate?> FindTemplateAsync(Guid organizationId, Guid id, CancellationToken ct);
    Task<IReadOnlyCollection<WhatsAppTemplate>> FindTemplatesByChannelAsync(Guid organizationId, Guid channelId, CancellationToken ct);
    Task<IReadOnlyCollection<NotificationRule>> ListRulesAsync(Guid organizationId, CancellationToken ct);
    Task<NotificationRule?> FindRuleAsync(Guid organizationId, Guid id, CancellationToken ct);
    Task<bool> RuleNameExistsAsync(Guid organizationId, string normalizedName, Guid? excludingId, CancellationToken ct);
    Task<IReadOnlyCollection<NotificationRuleSchedule>> ListSchedulesAsync(Guid organizationId, Guid ruleId, CancellationToken ct);
    Task<DocumentType?> FindDocumentTypeAsync(Guid organizationId, Guid id, CancellationToken ct);
    Task<IReadOnlyCollection<DocumentType>> ListDocumentTypesAsync(Guid organizationId, CancellationToken ct);
    Task<DocumentCategory?> FindCategoryAsync(Guid organizationId, Guid id, CancellationToken ct);
    Task<Organization?> FindOrganizationAsync(Guid organizationId, CancellationToken ct);
    Task<UserAccount?> FindUserAccountAsync(Guid organizationId, Guid id, CancellationToken ct);
    Task<IReadOnlyCollection<NotificationEventResult>> ListEventsAsync(Guid organizationId, int limit, CancellationToken ct);
    Task<IReadOnlyCollection<NotificationQueueResult>> ListQueueAsync(Guid organizationId, string? status, int limit, CancellationToken ct);
    void AddChannel(WhatsAppChannel channel);
    void AddTemplate(WhatsAppTemplate template);
    void AddRule(NotificationRule rule);
    void AddSchedule(NotificationRuleSchedule schedule);
    Task SaveChangesAsync(CancellationToken ct);
}

public interface INotificationService
{
    Task<IReadOnlyCollection<WhatsAppChannelResult>> ListChannelsAsync(CurrentAccount actor, CancellationToken ct);
    Task<WhatsAppChannelResult> CreateChannelAsync(WhatsAppChannelInput input, CurrentAccount actor, CancellationToken ct);
    Task<WhatsAppChannelResult> UpdateChannelAsync(Guid id, WhatsAppChannelInput input, CurrentAccount actor, CancellationToken ct);
    Task<WhatsAppChannelResult> SetChannelActiveAsync(Guid id, bool active, CurrentAccount actor, CancellationToken ct);
    Task<WhatsAppConnectionResult> TestChannelAsync(Guid id, CurrentAccount actor, CancellationToken ct);
    Task<TemplateSyncResult> SyncTemplatesAsync(Guid id, CurrentAccount actor, CancellationToken ct);
    Task<IReadOnlyCollection<WhatsAppTemplateResult>> ListTemplatesAsync(Guid? channelId, string? status, string? search, CurrentAccount actor, CancellationToken ct);
    Task<IReadOnlyCollection<NotificationRuleResult>> ListRulesAsync(CurrentAccount actor, CancellationToken ct);
    Task<NotificationRuleResult> CreateRuleAsync(NotificationRuleInput input, CurrentAccount actor, CancellationToken ct);
    Task<NotificationRuleResult> UpdateRuleAsync(Guid id, NotificationRuleInput input, CurrentAccount actor, CancellationToken ct);
    Task<NotificationRuleResult> SetRuleActiveAsync(Guid id, bool active, CurrentAccount actor, CancellationToken ct);
    Task<IReadOnlyCollection<NotificationEventResult>> ListEventsAsync(int limit, CurrentAccount actor, CancellationToken ct);
    Task<IReadOnlyCollection<NotificationQueueResult>> ListQueueAsync(string? status, int limit, CurrentAccount actor, CancellationToken ct);
    Task<NotificationContactResult> GetContactAsync(CurrentAccount actor, CancellationToken ct);
    Task<NotificationContactResult> UpdateContactAsync(NotificationContactInput input, CurrentAccount actor, CancellationToken ct);
    Task<NotificationSettingsResult> GetSettingsAsync(CurrentAccount actor, CancellationToken ct);
    Task<NotificationSettingsResult> UpdateSettingsAsync(NotificationSettingsInput input, CurrentAccount actor, CancellationToken ct);
}

public interface IWhatsAppWebhookService
{
    Task<bool> VerifyAsync(string verifyToken, CancellationToken ct);
    Task<bool> ProcessAsync(byte[] body, string? signature, CancellationToken ct);
}
