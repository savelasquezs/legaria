namespace Legaria.Domain.Notifications;

public static class NotificationCodes
{
    public const string DocumentExpiring = "DOCUMENT_EXPIRING";
    public const string WhatsApp = "WHATSAPP";
    public const string Active = "ACTIVE";
    public const string Inactive = "INACTIVE";
    public const string Unverified = "UNVERIFIED";
    public const string Connected = "CONNECTED";
    public const string Error = "ERROR";
    public const string Pending = "PENDING";
    public const string Processing = "PROCESSING";
    public const string Sent = "SENT";
    public const string Failed = "FAILED";
    public const string Cancelled = "CANCELLED";
}

public sealed class WhatsAppChannel
{
    private WhatsAppChannel() { }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string NormalizedName { get; private set; } = string.Empty;
    public string PhoneNumberId { get; private set; } = string.Empty;
    public string BusinessAccountId { get; private set; } = string.Empty;
    public string EncryptedAccessToken { get; private set; } = string.Empty;
    public string WebhookVerifyTokenHash { get; private set; } = string.Empty;
    public string EncryptedAppSecret { get; private set; } = string.Empty;
    public string? DisplayPhoneNumber { get; private set; }
    public string Status { get; private set; } = NotificationCodes.Active;
    public string ConnectionStatus { get; private set; } = NotificationCodes.Unverified;
    public DateTimeOffset? LastVerifiedAt { get; private set; }
    public DateTimeOffset? LastSynchronizedAt { get; private set; }
    public string? LastError { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public static WhatsAppChannel Create(Guid organizationId, string name, string normalizedName,
        string phoneNumberId, string businessAccountId, string encryptedAccessToken,
        string verifyTokenHash, string encryptedAppSecret, DateTimeOffset now) => new()
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            Name = name,
            NormalizedName = normalizedName,
            PhoneNumberId = phoneNumberId,
            BusinessAccountId = businessAccountId,
            EncryptedAccessToken = encryptedAccessToken,
            WebhookVerifyTokenHash = verifyTokenHash,
            EncryptedAppSecret = encryptedAppSecret,
            CreatedAt = now,
            UpdatedAt = now
        };

    public void Update(string name, string normalizedName, string phoneNumberId, string businessAccountId,
        string? encryptedAccessToken, string? verifyTokenHash, string? encryptedAppSecret, DateTimeOffset now)
    {
        var credentialsChanged = PhoneNumberId != phoneNumberId || BusinessAccountId != businessAccountId ||
            encryptedAccessToken is not null || verifyTokenHash is not null || encryptedAppSecret is not null;
        Name = name; NormalizedName = normalizedName; PhoneNumberId = phoneNumberId;
        BusinessAccountId = businessAccountId;
        if (encryptedAccessToken is not null) EncryptedAccessToken = encryptedAccessToken;
        if (verifyTokenHash is not null) WebhookVerifyTokenHash = verifyTokenHash;
        if (encryptedAppSecret is not null) EncryptedAppSecret = encryptedAppSecret;
        if (credentialsChanged) ConnectionStatus = NotificationCodes.Unverified;
        LastError = null; UpdatedAt = now;
    }

    public void SetActive(bool active, DateTimeOffset now) { Status = active ? NotificationCodes.Active : NotificationCodes.Inactive; UpdatedAt = now; }
    public void ConnectionSucceeded(string? displayPhoneNumber, DateTimeOffset now)
    { ConnectionStatus = NotificationCodes.Connected; DisplayPhoneNumber = displayPhoneNumber; LastVerifiedAt = now; LastError = null; UpdatedAt = now; }
    public void ConnectionFailed(string error, DateTimeOffset now)
    { ConnectionStatus = NotificationCodes.Error; LastVerifiedAt = now; LastError = error; UpdatedAt = now; }
    public void Synchronized(DateTimeOffset now) { LastSynchronizedAt = now; LastError = null; UpdatedAt = now; }
}

public sealed class WhatsAppTemplate
{
    private WhatsAppTemplate() { }
    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid WhatsAppChannelId { get; private set; }
    public string MetaTemplateId { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string Category { get; private set; } = string.Empty;
    public string Language { get; private set; } = string.Empty;
    public string Status { get; private set; } = string.Empty;
    public string ComponentsJson { get; private set; } = "[]";
    public string VariablesJson { get; private set; } = "[]";
    public string ButtonsJson { get; private set; } = "[]";
    public string ContentHash { get; private set; } = string.Empty;
    public bool IsAvailable { get; private set; }
    public DateTimeOffset LastSynchronizedAt { get; private set; }
    public DateTimeOffset? LastChangedAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public static WhatsAppTemplate Create(Guid organizationId, Guid channelId, string metaId, string name,
        string category, string language, string status, string components, string variables, string buttons,
        string hash, DateTimeOffset now) => new()
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            WhatsAppChannelId = channelId,
            MetaTemplateId = metaId,
            Name = name,
            Category = category,
            Language = language,
            Status = status,
            ComponentsJson = components,
            VariablesJson = variables,
            ButtonsJson = buttons,
            ContentHash = hash,
            IsAvailable = true,
            LastSynchronizedAt = now,
            LastChangedAt = now,
            CreatedAt = now,
            UpdatedAt = now
        };

    public void Synchronize(string name, string category, string language, string status, string components,
        string variables, string buttons, string hash, DateTimeOffset now)
    {
        if (ContentHash != hash || Status != status) LastChangedAt = now;
        Name = name; Category = category; Language = language; Status = status; ComponentsJson = components;
        VariablesJson = variables; ButtonsJson = buttons; ContentHash = hash; IsAvailable = true;
        LastSynchronizedAt = now; UpdatedAt = now;
    }
    public void MarkUnavailable(DateTimeOffset now) { IsAvailable = false; LastSynchronizedAt = now; LastChangedAt = now; UpdatedAt = now; }
}

public sealed class NotificationRule
{
    private NotificationRule() { }
    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string NormalizedName { get; private set; } = string.Empty;
    public string EventCode { get; private set; } = NotificationCodes.DocumentExpiring;
    public Guid DocumentTypeId { get; private set; }
    public Guid WhatsAppChannelId { get; private set; }
    public Guid WhatsAppTemplateId { get; private set; }
    public string Priority { get; private set; } = "NORMAL";
    public string Status { get; private set; } = NotificationCodes.Inactive;
    public string RecipientsJson { get; private set; } = "[]";
    public string VariableMappingsJson { get; private set; } = "{}";
    public string TemplateContentHash { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public static NotificationRule Create(Guid organizationId, string name, string normalizedName,
        Guid documentTypeId, Guid channelId, Guid templateId, string priority, string recipients,
        string mappings, string templateHash, DateTimeOffset now) => new()
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            Name = name,
            NormalizedName = normalizedName,
            DocumentTypeId = documentTypeId,
            WhatsAppChannelId = channelId,
            WhatsAppTemplateId = templateId,
            Priority = priority,
            RecipientsJson = recipients,
            VariableMappingsJson = mappings,
            TemplateContentHash = templateHash,
            CreatedAt = now,
            UpdatedAt = now
        };

    public void Update(string name, string normalizedName, Guid documentTypeId, Guid channelId, Guid templateId,
        string priority, string recipients, string mappings, string templateHash, DateTimeOffset now)
    {
        Name = name; NormalizedName = normalizedName; DocumentTypeId = documentTypeId; WhatsAppChannelId = channelId;
        WhatsAppTemplateId = templateId; Priority = priority; RecipientsJson = recipients;
        VariableMappingsJson = mappings; TemplateContentHash = templateHash; UpdatedAt = now;
    }
    public void SetActive(bool active, DateTimeOffset now) { Status = active ? NotificationCodes.Active : NotificationCodes.Inactive; UpdatedAt = now; }
}

public sealed class NotificationRuleSchedule
{
    private NotificationRuleSchedule() { }
    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid NotificationRuleId { get; private set; }
    public int Amount { get; private set; }
    public string Unit { get; private set; } = string.Empty;
    public bool IsActive { get; private set; } = true;
    public static NotificationRuleSchedule Create(Guid organizationId, Guid ruleId, int amount, string unit) =>
        new() { Id = Guid.NewGuid(), OrganizationId = organizationId, NotificationRuleId = ruleId, Amount = amount, Unit = unit };
    public DateOnly ScheduledOn(DateOnly expiration) => Unit switch
    {
        "DAY" => expiration.AddDays(-Amount),
        "WEEK" => expiration.AddDays(-7 * Amount),
        "MONTH" => expiration.AddMonths(-Amount),
        _ => throw new InvalidOperationException("Unidad de anticipación inválida.")
    };
    public void Deactivate() => IsActive = false;
}

public sealed class NotificationEvent
{
    private NotificationEvent() { }
    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid NotificationRuleId { get; private set; }
    public Guid EmployeeDocumentId { get; private set; }
    public Guid NotificationRuleScheduleId { get; private set; }
    public string EventCode { get; private set; } = NotificationCodes.DocumentExpiring;
    public string OccurrenceKey { get; private set; } = string.Empty;
    public string PayloadJson { get; private set; } = "{}";
    public DateTimeOffset OccurredAt { get; private set; }
    public static NotificationEvent Create(Guid organizationId, Guid ruleId, Guid documentId, Guid scheduleId,
        string key, string payload, DateTimeOffset now) => new()
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            NotificationRuleId = ruleId,
            EmployeeDocumentId = documentId,
            NotificationRuleScheduleId = scheduleId,
            OccurrenceKey = key,
            PayloadJson = payload,
            OccurredAt = now
        };
}

public sealed class NotificationQueueItem
{
    private NotificationQueueItem() { }
    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid NotificationEventId { get; private set; }
    public Guid WhatsAppChannelId { get; private set; }
    public string Channel { get; private set; } = NotificationCodes.WhatsApp;
    public string RecipientType { get; private set; } = string.Empty;
    public Guid? RecipientId { get; private set; }
    public string? Destination { get; private set; }
    public string DeduplicationKey { get; private set; } = string.Empty;
    public string Priority { get; private set; } = "NORMAL";
    public string PayloadJson { get; private set; } = "{}";
    public string Status { get; private set; } = NotificationCodes.Pending;
    public string? DeliveryStatus { get; private set; }
    public int AttemptCount { get; private set; }
    public DateTimeOffset NextAttemptAt { get; private set; }
    public DateTimeOffset? LockedAt { get; private set; }
    public string? WorkerId { get; private set; }
    public string? ProviderMessageId { get; private set; }
    public string? LastError { get; private set; }
    public DateTimeOffset? SentAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public static NotificationQueueItem Create(Guid organizationId, Guid eventId, Guid channelId,
        string recipientType, Guid? recipientId, string? destination, string deduplicationKey,
        string priority, string payload, DateTimeOffset now, string? cancellationReason = null) => new()
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            NotificationEventId = eventId,
            WhatsAppChannelId = channelId,
            RecipientType = recipientType,
            RecipientId = recipientId,
            Destination = destination,
            DeduplicationKey = deduplicationKey,
            Priority = priority,
            PayloadJson = payload,
            Status = cancellationReason is null ? NotificationCodes.Pending : NotificationCodes.Cancelled,
            LastError = cancellationReason,
            NextAttemptAt = now,
            CreatedAt = now,
            UpdatedAt = now
        };

    public void Claim(string workerId, DateTimeOffset now) { Status = NotificationCodes.Processing; WorkerId = workerId; LockedAt = now; UpdatedAt = now; }
    public void Sent(string messageId, DateTimeOffset now) { Status = NotificationCodes.Sent; DeliveryStatus = "ACCEPTED"; ProviderMessageId = messageId; SentAt = now; LockedAt = null; WorkerId = null; UpdatedAt = now; }
    public void Retry(string error, DateTimeOffset next, DateTimeOffset now) { Status = NotificationCodes.Pending; AttemptCount++; LastError = error; NextAttemptAt = next; LockedAt = null; WorkerId = null; UpdatedAt = now; }
    public void Fail(string error, DateTimeOffset now) { Status = NotificationCodes.Failed; AttemptCount++; LastError = error; LockedAt = null; WorkerId = null; UpdatedAt = now; }
    public void Cancel(string reason, DateTimeOffset now) { Status = NotificationCodes.Cancelled; LastError = reason; LockedAt = null; WorkerId = null; UpdatedAt = now; }
    public void RecordAttempt() => AttemptCount++;
    public void UpdateDelivery(string status, string? error, DateTimeOffset now) { DeliveryStatus = status; if (error is not null) LastError = error; UpdatedAt = now; }
}

public sealed class NotificationDeliveryAttempt
{
    private NotificationDeliveryAttempt() { }
    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid NotificationQueueItemId { get; private set; }
    public int AttemptNumber { get; private set; }
    public string RequestJson { get; private set; } = "{}";
    public string? ResponseJson { get; private set; }
    public string Outcome { get; private set; } = string.Empty;
    public string? ErrorCode { get; private set; }
    public DateTimeOffset StartedAt { get; private set; }
    public DateTimeOffset FinishedAt { get; private set; }
    public static NotificationDeliveryAttempt Create(Guid organizationId, Guid itemId, int number,
        string request, string? response, string outcome, string? errorCode, DateTimeOffset started, DateTimeOffset finished) =>
        new()
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            NotificationQueueItemId = itemId,
            AttemptNumber = number,
            RequestJson = request,
            ResponseJson = response,
            Outcome = outcome,
            ErrorCode = errorCode,
            StartedAt = started,
            FinishedAt = finished
        };
}

public sealed class WhatsAppWebhookReceipt
{
    private WhatsAppWebhookReceipt() { }
    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid WhatsAppChannelId { get; private set; }
    public string EventKey { get; private set; } = string.Empty;
    public DateTimeOffset ReceivedAt { get; private set; }
    public static WhatsAppWebhookReceipt Create(Guid organizationId, Guid channelId, string key, DateTimeOffset now) =>
        new() { Id = Guid.NewGuid(), OrganizationId = organizationId, WhatsAppChannelId = channelId, EventKey = key, ReceivedAt = now };
}
