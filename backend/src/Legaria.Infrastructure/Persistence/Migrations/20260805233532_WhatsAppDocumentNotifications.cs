using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Legaria.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class WhatsAppDocumentNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_employee_documents_organization_id_document_type_id",
                table: "employee_documents");

            migrationBuilder.AddColumn<string>(
                name: "mobile_phone",
                table: "user_accounts",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "whats_app_consent_at",
                table: "user_accounts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "notification_time",
                table: "organizations",
                type: "time without time zone",
                nullable: false,
                defaultValue: new TimeOnly(8, 0, 0));

            migrationBuilder.AddColumn<string>(
                name: "time_zone_id",
                table: "organizations",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "America/Bogota");

            migrationBuilder.AddColumn<string>(
                name: "contact_email",
                table: "employees",
                type: "character varying(320)",
                maxLength: 320,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "mobile_phone",
                table: "employees",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "whats_app_consent_at",
                table: "employees",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "whatsapp_channels",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    normalized_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    phone_number_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    business_account_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    encrypted_access_token = table.Column<string>(type: "text", nullable: false),
                    webhook_verify_token_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    encrypted_app_secret = table.Column<string>(type: "text", nullable: false),
                    display_phone_number = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    connection_status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    last_verified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_synchronized_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_error = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_whatsapp_channels", x => x.id);
                    table.UniqueConstraint("ak_whats_app_channels_organization_id_id", x => new { x.organization_id, x.id });
                    table.CheckConstraint("ck_whatsapp_channels_connection", "connection_status IN ('UNVERIFIED', 'CONNECTED', 'ERROR')");
                    table.CheckConstraint("ck_whatsapp_channels_status", "status IN ('ACTIVE', 'INACTIVE')");
                    table.ForeignKey(
                        name: "fk_whatsapp_channels_organizations_organization_id",
                        column: x => x.organization_id,
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "whatsapp_templates",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    whats_app_channel_id = table.Column<Guid>(type: "uuid", nullable: false),
                    meta_template_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    category = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    language = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    components_json = table.Column<string>(type: "jsonb", nullable: false),
                    variables_json = table.Column<string>(type: "jsonb", nullable: false),
                    buttons_json = table.Column<string>(type: "jsonb", nullable: false),
                    content_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    is_available = table.Column<bool>(type: "boolean", nullable: false),
                    last_synchronized_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_changed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_whatsapp_templates", x => x.id);
                    table.UniqueConstraint("ak_whats_app_templates_organization_id_id", x => new { x.organization_id, x.id });
                    table.ForeignKey(
                        name: "fk_whatsapp_templates_whatsapp_channels_organization_id_whats_",
                        columns: x => new { x.organization_id, x.whats_app_channel_id },
                        principalTable: "whatsapp_channels",
                        principalColumns: new[] { "organization_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "whatsapp_webhook_receipts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    whats_app_channel_id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_key = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    received_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_whatsapp_webhook_receipts", x => x.id);
                    table.ForeignKey(
                        name: "fk_whatsapp_webhook_receipts_whatsapp_channels_organization_id",
                        columns: x => new { x.organization_id, x.whats_app_channel_id },
                        principalTable: "whatsapp_channels",
                        principalColumns: new[] { "organization_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "notification_rules",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    normalized_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    event_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    document_type_id = table.Column<Guid>(type: "uuid", nullable: false),
                    whats_app_channel_id = table.Column<Guid>(type: "uuid", nullable: false),
                    whats_app_template_id = table.Column<Guid>(type: "uuid", nullable: false),
                    priority = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    recipients_json = table.Column<string>(type: "jsonb", nullable: false),
                    variable_mappings_json = table.Column<string>(type: "jsonb", nullable: false),
                    template_content_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notification_rules", x => x.id);
                    table.UniqueConstraint("ak_notification_rules_organization_id_id", x => new { x.organization_id, x.id });
                    table.CheckConstraint("ck_notification_rules_event", "event_code = 'DOCUMENT_EXPIRING'");
                    table.CheckConstraint("ck_notification_rules_priority", "priority IN ('LOW', 'NORMAL', 'HIGH', 'CRITICAL')");
                    table.CheckConstraint("ck_notification_rules_status", "status IN ('ACTIVE', 'INACTIVE')");
                    table.ForeignKey(
                        name: "fk_notification_rules_document_types_organization_id_document_",
                        columns: x => new { x.organization_id, x.document_type_id },
                        principalTable: "document_types",
                        principalColumns: new[] { "organization_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_notification_rules_whats_app_channels_organization_id_whats",
                        columns: x => new { x.organization_id, x.whats_app_channel_id },
                        principalTable: "whatsapp_channels",
                        principalColumns: new[] { "organization_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_notification_rules_whats_app_templates_organization_id_what",
                        columns: x => new { x.organization_id, x.whats_app_template_id },
                        principalTable: "whatsapp_templates",
                        principalColumns: new[] { "organization_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "notification_rule_schedules",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    notification_rule_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount = table.Column<int>(type: "integer", nullable: false),
                    unit = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notification_rule_schedules", x => x.id);
                    table.UniqueConstraint("ak_notification_rule_schedules_organization_id_id", x => new { x.organization_id, x.id });
                    table.CheckConstraint("ck_notification_rule_schedules_amount", "amount > 0");
                    table.CheckConstraint("ck_notification_rule_schedules_unit", "unit IN ('DAY', 'WEEK', 'MONTH')");
                    table.ForeignKey(
                        name: "fk_notification_rule_schedules_notification_rules_organization",
                        columns: x => new { x.organization_id, x.notification_rule_id },
                        principalTable: "notification_rules",
                        principalColumns: new[] { "organization_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "notification_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    notification_rule_id = table.Column<Guid>(type: "uuid", nullable: false),
                    employee_document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    notification_rule_schedule_id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    occurrence_key = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    payload_json = table.Column<string>(type: "jsonb", nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notification_events", x => x.id);
                    table.UniqueConstraint("ak_notification_events_organization_id_id", x => new { x.organization_id, x.id });
                    table.ForeignKey(
                        name: "fk_notification_events_employee_documents_organization_id_empl",
                        columns: x => new { x.organization_id, x.employee_document_id },
                        principalTable: "employee_documents",
                        principalColumns: new[] { "organization_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_notification_events_notification_rule_schedules_organizatio",
                        columns: x => new { x.organization_id, x.notification_rule_schedule_id },
                        principalTable: "notification_rule_schedules",
                        principalColumns: new[] { "organization_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_notification_events_notification_rules_organization_id_noti",
                        columns: x => new { x.organization_id, x.notification_rule_id },
                        principalTable: "notification_rules",
                        principalColumns: new[] { "organization_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "notification_queue",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    notification_event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    whats_app_channel_id = table.Column<Guid>(type: "uuid", nullable: false),
                    channel = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    recipient_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    recipient_id = table.Column<Guid>(type: "uuid", nullable: true),
                    destination = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    deduplication_key = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    priority = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    payload_json = table.Column<string>(type: "jsonb", nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    delivery_status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    attempt_count = table.Column<int>(type: "integer", nullable: false),
                    next_attempt_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    locked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    worker_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    provider_message_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    last_error = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    sent_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notification_queue", x => x.id);
                    table.UniqueConstraint("ak_notification_queue_items_organization_id_id", x => new { x.organization_id, x.id });
                    table.CheckConstraint("ck_notification_queue_channel", "channel = 'WHATSAPP'");
                    table.CheckConstraint("ck_notification_queue_status", "status IN ('PENDING', 'PROCESSING', 'SENT', 'FAILED', 'CANCELLED')");
                    table.ForeignKey(
                        name: "fk_notification_queue_notification_events_organization_id_noti",
                        columns: x => new { x.organization_id, x.notification_event_id },
                        principalTable: "notification_events",
                        principalColumns: new[] { "organization_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_notification_queue_whats_app_channels_organization_id_whats",
                        columns: x => new { x.organization_id, x.whats_app_channel_id },
                        principalTable: "whatsapp_channels",
                        principalColumns: new[] { "organization_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "notification_delivery_attempts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    notification_queue_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    attempt_number = table.Column<int>(type: "integer", nullable: false),
                    request_json = table.Column<string>(type: "jsonb", nullable: false),
                    response_json = table.Column<string>(type: "jsonb", nullable: true),
                    outcome = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    error_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    finished_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notification_delivery_attempts", x => x.id);
                    table.ForeignKey(
                        name: "fk_notification_delivery_attempts_notification_queue_items_org",
                        columns: x => new { x.organization_id, x.notification_queue_item_id },
                        principalTable: "notification_queue",
                        principalColumns: new[] { "organization_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_employee_documents_organization_id_document_type_id_expires",
                table: "employee_documents",
                columns: new[] { "organization_id", "document_type_id", "expires_on" },
                filter: "replaced_at IS NULL AND expires_on IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_notification_delivery_attempts_notification_queue_item_id_a",
                table: "notification_delivery_attempts",
                columns: new[] { "notification_queue_item_id", "attempt_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_notification_delivery_attempts_organization_id_notification",
                table: "notification_delivery_attempts",
                columns: new[] { "organization_id", "notification_queue_item_id" });

            migrationBuilder.CreateIndex(
                name: "ix_notification_events_occurrence_key",
                table: "notification_events",
                column: "occurrence_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_notification_events_organization_id_employee_document_id",
                table: "notification_events",
                columns: new[] { "organization_id", "employee_document_id" });

            migrationBuilder.CreateIndex(
                name: "ix_notification_events_organization_id_notification_rule_id",
                table: "notification_events",
                columns: new[] { "organization_id", "notification_rule_id" });

            migrationBuilder.CreateIndex(
                name: "ix_notification_events_organization_id_notification_rule_sched",
                table: "notification_events",
                columns: new[] { "organization_id", "notification_rule_schedule_id" });

            migrationBuilder.CreateIndex(
                name: "ix_notification_events_organization_id_occurred_at",
                table: "notification_events",
                columns: new[] { "organization_id", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "ix_notification_queue_deduplication_key",
                table: "notification_queue",
                column: "deduplication_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_notification_queue_organization_id_notification_event_id",
                table: "notification_queue",
                columns: new[] { "organization_id", "notification_event_id" });

            migrationBuilder.CreateIndex(
                name: "ix_notification_queue_organization_id_whats_app_channel_id",
                table: "notification_queue",
                columns: new[] { "organization_id", "whats_app_channel_id" });

            migrationBuilder.CreateIndex(
                name: "ix_notification_queue_provider_message_id",
                table: "notification_queue",
                column: "provider_message_id");

            migrationBuilder.CreateIndex(
                name: "ix_notification_queue_status_next_attempt_at_priority",
                table: "notification_queue",
                columns: new[] { "status", "next_attempt_at", "priority" });

            migrationBuilder.CreateIndex(
                name: "ix_notification_rule_schedules_notification_rule_id_amount_unit",
                table: "notification_rule_schedules",
                columns: new[] { "notification_rule_id", "amount", "unit" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_notification_rule_schedules_organization_id_notification_ru",
                table: "notification_rule_schedules",
                columns: new[] { "organization_id", "notification_rule_id" });

            migrationBuilder.CreateIndex(
                name: "ix_notification_rules_organization_id_document_type_id",
                table: "notification_rules",
                columns: new[] { "organization_id", "document_type_id" });

            migrationBuilder.CreateIndex(
                name: "ix_notification_rules_organization_id_normalized_name",
                table: "notification_rules",
                columns: new[] { "organization_id", "normalized_name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_notification_rules_organization_id_status_event_code",
                table: "notification_rules",
                columns: new[] { "organization_id", "status", "event_code" });

            migrationBuilder.CreateIndex(
                name: "ix_notification_rules_organization_id_whats_app_channel_id",
                table: "notification_rules",
                columns: new[] { "organization_id", "whats_app_channel_id" });

            migrationBuilder.CreateIndex(
                name: "ix_notification_rules_organization_id_whats_app_template_id",
                table: "notification_rules",
                columns: new[] { "organization_id", "whats_app_template_id" });

            migrationBuilder.CreateIndex(
                name: "ix_whatsapp_channels_organization_id_normalized_name",
                table: "whatsapp_channels",
                columns: new[] { "organization_id", "normalized_name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_whatsapp_channels_phone_number_id",
                table: "whatsapp_channels",
                column: "phone_number_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_whatsapp_channels_webhook_verify_token_hash",
                table: "whatsapp_channels",
                column: "webhook_verify_token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_whatsapp_templates_organization_id_status_name",
                table: "whatsapp_templates",
                columns: new[] { "organization_id", "status", "name" });

            migrationBuilder.CreateIndex(
                name: "ix_whatsapp_templates_organization_id_whats_app_channel_id",
                table: "whatsapp_templates",
                columns: new[] { "organization_id", "whats_app_channel_id" });

            migrationBuilder.CreateIndex(
                name: "ix_whatsapp_templates_whats_app_channel_id_meta_template_id",
                table: "whatsapp_templates",
                columns: new[] { "whats_app_channel_id", "meta_template_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_whatsapp_webhook_receipts_event_key",
                table: "whatsapp_webhook_receipts",
                column: "event_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_whatsapp_webhook_receipts_organization_id_received_at",
                table: "whatsapp_webhook_receipts",
                columns: new[] { "organization_id", "received_at" });

            migrationBuilder.CreateIndex(
                name: "ix_whatsapp_webhook_receipts_organization_id_whats_app_channel",
                table: "whatsapp_webhook_receipts",
                columns: new[] { "organization_id", "whats_app_channel_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "notification_delivery_attempts");

            migrationBuilder.DropTable(
                name: "whatsapp_webhook_receipts");

            migrationBuilder.DropTable(
                name: "notification_queue");

            migrationBuilder.DropTable(
                name: "notification_events");

            migrationBuilder.DropTable(
                name: "notification_rule_schedules");

            migrationBuilder.DropTable(
                name: "notification_rules");

            migrationBuilder.DropTable(
                name: "whatsapp_templates");

            migrationBuilder.DropTable(
                name: "whatsapp_channels");

            migrationBuilder.DropIndex(
                name: "ix_employee_documents_organization_id_document_type_id_expires",
                table: "employee_documents");

            migrationBuilder.DropColumn(
                name: "mobile_phone",
                table: "user_accounts");

            migrationBuilder.DropColumn(
                name: "whats_app_consent_at",
                table: "user_accounts");

            migrationBuilder.DropColumn(
                name: "notification_time",
                table: "organizations");

            migrationBuilder.DropColumn(
                name: "time_zone_id",
                table: "organizations");

            migrationBuilder.DropColumn(
                name: "contact_email",
                table: "employees");

            migrationBuilder.DropColumn(
                name: "mobile_phone",
                table: "employees");

            migrationBuilder.DropColumn(
                name: "whats_app_consent_at",
                table: "employees");

            migrationBuilder.CreateIndex(
                name: "ix_employee_documents_organization_id_document_type_id",
                table: "employee_documents",
                columns: new[] { "organization_id", "document_type_id" });
        }
    }
}
