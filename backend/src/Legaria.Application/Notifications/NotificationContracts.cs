namespace Legaria.Application.Notifications;

public sealed record WhatsAppChannelInput(string Name, string PhoneNumberId, string BusinessAccountId,
    string? AccessToken, string? WebhookVerifyToken, string? AppSecret);
public sealed record WhatsAppChannelResult(Guid Id, string Name, string PhoneNumberId, string BusinessAccountId,
    string? DisplayPhoneNumber, string Status, string ConnectionStatus, bool AccessTokenConfigured,
    bool WebhookVerifyTokenConfigured, bool AppSecretConfigured, DateTimeOffset? LastVerifiedAt,
    DateTimeOffset? LastSynchronizedAt, string? LastError);
public sealed record WhatsAppTemplateResult(Guid Id, Guid WhatsAppChannelId, string Name, string Category,
    string Language, string Status, string ComponentsJson, IReadOnlyCollection<string> Variables,
    string ButtonsJson, bool IsAvailable, DateTimeOffset LastSynchronizedAt, DateTimeOffset? LastChangedAt);
public sealed record NotificationScheduleInput(int Amount, string Unit);
public sealed record NotificationRuleInput(string Name, Guid DocumentTypeId, Guid WhatsAppChannelId,
    Guid WhatsAppTemplateId, string Priority, IReadOnlyCollection<string> Recipients,
    IReadOnlyDictionary<string, string> VariableMappings, IReadOnlyCollection<NotificationScheduleInput> Schedules);
public sealed record NotificationRuleResult(Guid Id, string Name, string EventCode, Guid DocumentTypeId,
    string DocumentTypeName, Guid WhatsAppChannelId, string ChannelName, Guid WhatsAppTemplateId,
    string TemplateName, string Priority, string Status, bool IsBlocked, string? BlockedReason,
    IReadOnlyCollection<string> Recipients, IReadOnlyDictionary<string, string> VariableMappings,
    IReadOnlyCollection<NotificationScheduleResult> Schedules);
public sealed record NotificationScheduleResult(Guid Id, int Amount, string Unit);
public sealed record NotificationEventResult(Guid Id, string EventCode, Guid NotificationRuleId, string RuleName,
    Guid EmployeeDocumentId, string DocumentName, string PayloadJson, DateTimeOffset OccurredAt);
public sealed record NotificationQueueResult(Guid Id, string EventCode, string RuleName, string DocumentName,
    string RecipientType, string? Destination, string Priority, string Status, string? DeliveryStatus,
    int AttemptCount, DateTimeOffset CreatedAt, DateTimeOffset? SentAt, string? LastError, string PayloadJson,
    IReadOnlyCollection<NotificationAttemptResult> Attempts);
public sealed record NotificationAttemptResult(int AttemptNumber, string Outcome, string? ErrorCode,
    string RequestJson, string? ResponseJson, DateTimeOffset StartedAt, DateTimeOffset FinishedAt);
public sealed record NotificationContactInput(string? MobilePhone, bool WhatsAppConsent);
public sealed record NotificationContactResult(string? MobilePhone, DateTimeOffset? WhatsAppConsentAt);
public sealed record NotificationSettingsInput(string TimeZoneId, TimeOnly NotificationTime);
public sealed record NotificationSettingsResult(string TimeZoneId, TimeOnly NotificationTime);
public sealed record WhatsAppConnectionResult(bool Success, string? DisplayPhoneNumber, string? Error);
public sealed record TemplateSyncResult(int Created, int Updated, int Unavailable, DateTimeOffset SynchronizedAt);

public static class NotificationErrorCodes
{
    public const string Forbidden = "notification.forbidden";
    public const string InvalidData = "notification.invalid_data";
    public const string NotFound = "notification.not_found";
    public const string DuplicateName = "notification.duplicate_name";
    public const string ChannelNotConnected = "whatsapp.channel_not_connected";
    public const string TemplateUnavailable = "whatsapp.template_unavailable";
    public const string ProviderError = "whatsapp.provider_error";
}

public enum NotificationErrorKind { Validation, NotFound, Conflict, Forbidden, Provider }
public sealed class NotificationException(string code, string message, NotificationErrorKind kind = NotificationErrorKind.Validation)
    : Exception(message)
{
    public string Code { get; } = code;
    public NotificationErrorKind Kind { get; } = kind;
}
