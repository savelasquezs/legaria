using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Legaria.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class NotificationRuleScheduleHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_notification_rule_schedules_notification_rule_id_amount_unit",
                table: "notification_rule_schedules");

            migrationBuilder.AddColumn<bool>(
                name: "is_active",
                table: "notification_rule_schedules",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.CreateIndex(
                name: "ix_notification_rule_schedules_notification_rule_id_amount_unit",
                table: "notification_rule_schedules",
                columns: new[] { "notification_rule_id", "amount", "unit" },
                unique: true,
                filter: "is_active");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_notification_rule_schedules_notification_rule_id_amount_unit",
                table: "notification_rule_schedules");

            migrationBuilder.DropColumn(
                name: "is_active",
                table: "notification_rule_schedules");

            migrationBuilder.CreateIndex(
                name: "ix_notification_rule_schedules_notification_rule_id_amount_unit",
                table: "notification_rule_schedules",
                columns: new[] { "notification_rule_id", "amount", "unit" },
                unique: true);
        }
    }
}
