using Legaria.Domain.Authentication;
using Legaria.Domain.Documents;
using Legaria.Domain.Notifications;
using Legaria.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Legaria.Infrastructure.Persistence;

internal sealed class WhatsAppChannelConfiguration : IEntityTypeConfiguration<WhatsAppChannel>
{
    public void Configure(EntityTypeBuilder<WhatsAppChannel> builder)
    {
        builder.ToTable("whatsapp_channels", table =>
        {
            table.HasCheckConstraint("ck_whatsapp_channels_status", "status IN ('ACTIVE', 'INACTIVE')");
            table.HasCheckConstraint("ck_whatsapp_channels_connection", "connection_status IN ('UNVERIFIED', 'CONNECTED', 'ERROR')");
        });
        builder.HasKey(x => x.Id);
        builder.HasAlternateKey(x => new { x.OrganizationId, x.Id });
        builder.Property(x => x.Name).HasMaxLength(150).IsRequired();
        builder.Property(x => x.NormalizedName).HasMaxLength(150).IsRequired();
        builder.Property(x => x.PhoneNumberId).HasMaxLength(64).IsRequired();
        builder.Property(x => x.BusinessAccountId).HasMaxLength(64).IsRequired();
        builder.Property(x => x.EncryptedAccessToken).HasColumnType("text").IsRequired();
        builder.Property(x => x.WebhookVerifyTokenHash).HasMaxLength(64).IsRequired();
        builder.Property(x => x.EncryptedAppSecret).HasColumnType("text").IsRequired();
        builder.Property(x => x.DisplayPhoneNumber).HasMaxLength(32);
        builder.Property(x => x.Status).HasMaxLength(16).IsRequired();
        builder.Property(x => x.ConnectionStatus).HasMaxLength(16).IsRequired();
        builder.Property(x => x.LastError).HasMaxLength(1000);
        builder.HasIndex(x => new { x.OrganizationId, x.NormalizedName }).IsUnique();
        builder.HasIndex(x => x.PhoneNumberId).IsUnique();
        builder.HasIndex(x => x.WebhookVerifyTokenHash).IsUnique();
        builder.HasOne<Organization>().WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class WhatsAppTemplateConfiguration : IEntityTypeConfiguration<WhatsAppTemplate>
{
    public void Configure(EntityTypeBuilder<WhatsAppTemplate> builder)
    {
        builder.ToTable("whatsapp_templates");
        builder.HasKey(x => x.Id);
        builder.HasAlternateKey(x => new { x.OrganizationId, x.Id });
        builder.Property(x => x.MetaTemplateId).HasMaxLength(128).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(255).IsRequired();
        builder.Property(x => x.Category).HasMaxLength(80).IsRequired();
        builder.Property(x => x.Language).HasMaxLength(20).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(40).IsRequired();
        builder.Property(x => x.ComponentsJson).HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.VariablesJson).HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.ButtonsJson).HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.ContentHash).HasMaxLength(64).IsRequired();
        builder.HasIndex(x => new { x.WhatsAppChannelId, x.MetaTemplateId }).IsUnique();
        builder.HasIndex(x => new { x.OrganizationId, x.Status, x.Name });
        builder.HasOne<WhatsAppChannel>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.WhatsAppChannelId })
            .HasPrincipalKey(x => new { x.OrganizationId, x.Id }).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class NotificationRuleConfiguration : IEntityTypeConfiguration<NotificationRule>
{
    public void Configure(EntityTypeBuilder<NotificationRule> builder)
    {
        builder.ToTable("notification_rules", table =>
        {
            table.HasCheckConstraint("ck_notification_rules_event", "event_code = 'DOCUMENT_EXPIRING'");
            table.HasCheckConstraint("ck_notification_rules_priority", "priority IN ('LOW', 'NORMAL', 'HIGH', 'CRITICAL')");
            table.HasCheckConstraint("ck_notification_rules_status", "status IN ('ACTIVE', 'INACTIVE')");
        });
        builder.HasKey(x => x.Id);
        builder.HasAlternateKey(x => new { x.OrganizationId, x.Id });
        builder.Property(x => x.Name).HasMaxLength(150).IsRequired();
        builder.Property(x => x.NormalizedName).HasMaxLength(150).IsRequired();
        builder.Property(x => x.EventCode).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Priority).HasMaxLength(16).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(16).IsRequired();
        builder.Property(x => x.RecipientsJson).HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.VariableMappingsJson).HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.TemplateContentHash).HasMaxLength(64).IsRequired();
        builder.HasIndex(x => new { x.OrganizationId, x.NormalizedName }).IsUnique();
        builder.HasIndex(x => new { x.OrganizationId, x.Status, x.EventCode });
        builder.HasOne<DocumentType>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.DocumentTypeId })
            .HasPrincipalKey(x => new { x.OrganizationId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<WhatsAppChannel>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.WhatsAppChannelId })
            .HasPrincipalKey(x => new { x.OrganizationId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<WhatsAppTemplate>().WithMany().HasForeignKey(x => new { x.OrganizationId, Id = x.WhatsAppTemplateId })
            .HasPrincipalKey(x => new { x.OrganizationId, x.Id }).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class NotificationRuleScheduleConfiguration : IEntityTypeConfiguration<NotificationRuleSchedule>
{
    public void Configure(EntityTypeBuilder<NotificationRuleSchedule> builder)
    {
        builder.ToTable("notification_rule_schedules", table =>
        {
            table.HasCheckConstraint("ck_notification_rule_schedules_amount", "amount > 0");
            table.HasCheckConstraint("ck_notification_rule_schedules_unit", "unit IN ('DAY', 'WEEK', 'MONTH')");
        });
        builder.HasKey(x => x.Id);
        builder.HasAlternateKey(x => new { x.OrganizationId, x.Id });
        builder.Property(x => x.IsActive).HasDefaultValue(true);
        builder.HasIndex(x => new { x.NotificationRuleId, x.Amount, x.Unit }).IsUnique().HasFilter("is_active");
        builder.HasOne<NotificationRule>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.NotificationRuleId })
            .HasPrincipalKey(x => new { x.OrganizationId, x.Id }).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class NotificationEventConfiguration : IEntityTypeConfiguration<NotificationEvent>
{
    public void Configure(EntityTypeBuilder<NotificationEvent> builder)
    {
        builder.ToTable("notification_events");
        builder.HasKey(x => x.Id);
        builder.HasAlternateKey(x => new { x.OrganizationId, x.Id });
        builder.Property(x => x.EventCode).HasMaxLength(50).IsRequired();
        builder.Property(x => x.OccurrenceKey).HasMaxLength(300).IsRequired();
        builder.Property(x => x.PayloadJson).HasColumnType("jsonb").IsRequired();
        builder.HasIndex(x => x.OccurrenceKey).IsUnique();
        builder.HasIndex(x => new { x.OrganizationId, x.OccurredAt });
        builder.HasOne<NotificationRule>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.NotificationRuleId })
            .HasPrincipalKey(x => new { x.OrganizationId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<EmployeeDocument>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.EmployeeDocumentId })
            .HasPrincipalKey(x => new { x.OrganizationId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<NotificationRuleSchedule>().WithMany().HasForeignKey(x => new { x.OrganizationId, Id = x.NotificationRuleScheduleId })
            .HasPrincipalKey(x => new { x.OrganizationId, x.Id }).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class NotificationQueueItemConfiguration : IEntityTypeConfiguration<NotificationQueueItem>
{
    public void Configure(EntityTypeBuilder<NotificationQueueItem> builder)
    {
        builder.ToTable("notification_queue", table =>
        {
            table.HasCheckConstraint("ck_notification_queue_status", "status IN ('PENDING', 'PROCESSING', 'SENT', 'FAILED', 'CANCELLED')");
            table.HasCheckConstraint("ck_notification_queue_channel", "channel = 'WHATSAPP'");
        });
        builder.HasKey(x => x.Id);
        builder.HasAlternateKey(x => new { x.OrganizationId, x.Id });
        builder.Property(x => x.Channel).HasMaxLength(20).IsRequired();
        builder.Property(x => x.RecipientType).HasMaxLength(30).IsRequired();
        builder.Property(x => x.Destination).HasMaxLength(16);
        builder.Property(x => x.DeduplicationKey).HasMaxLength(400).IsRequired();
        builder.Property(x => x.Priority).HasMaxLength(16).IsRequired();
        builder.Property(x => x.PayloadJson).HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.Status).HasMaxLength(16).IsRequired();
        builder.Property(x => x.DeliveryStatus).HasMaxLength(20);
        builder.Property(x => x.WorkerId).HasMaxLength(100);
        builder.Property(x => x.ProviderMessageId).HasMaxLength(200);
        builder.Property(x => x.LastError).HasMaxLength(1000);
        builder.HasIndex(x => x.DeduplicationKey).IsUnique();
        builder.HasIndex(x => new { x.Status, x.NextAttemptAt, x.Priority });
        builder.HasIndex(x => x.ProviderMessageId);
        builder.HasOne<NotificationEvent>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.NotificationEventId })
            .HasPrincipalKey(x => new { x.OrganizationId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<WhatsAppChannel>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.WhatsAppChannelId })
            .HasPrincipalKey(x => new { x.OrganizationId, x.Id }).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class NotificationDeliveryAttemptConfiguration : IEntityTypeConfiguration<NotificationDeliveryAttempt>
{
    public void Configure(EntityTypeBuilder<NotificationDeliveryAttempt> builder)
    {
        builder.ToTable("notification_delivery_attempts");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.RequestJson).HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.ResponseJson).HasColumnType("jsonb");
        builder.Property(x => x.Outcome).HasMaxLength(20).IsRequired();
        builder.Property(x => x.ErrorCode).HasMaxLength(100);
        builder.HasIndex(x => new { x.NotificationQueueItemId, x.AttemptNumber }).IsUnique();
        builder.HasOne<NotificationQueueItem>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.NotificationQueueItemId })
            .HasPrincipalKey(x => new { x.OrganizationId, x.Id }).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class WhatsAppWebhookReceiptConfiguration : IEntityTypeConfiguration<WhatsAppWebhookReceipt>
{
    public void Configure(EntityTypeBuilder<WhatsAppWebhookReceipt> builder)
    {
        builder.ToTable("whatsapp_webhook_receipts");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.EventKey).HasMaxLength(300).IsRequired();
        builder.HasIndex(x => x.EventKey).IsUnique();
        builder.HasIndex(x => new { x.OrganizationId, x.ReceivedAt });
        builder.HasOne<WhatsAppChannel>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.WhatsAppChannelId })
            .HasPrincipalKey(x => new { x.OrganizationId, x.Id }).OnDelete(DeleteBehavior.Restrict);
    }
}
