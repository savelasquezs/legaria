using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Legaria.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BranchesAndBranchAdministrators : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "actor_user_account_id",
                table: "security_audit_events",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "branch_id",
                table: "security_audit_events",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddUniqueConstraint(
                name: "ak_user_accounts_organization_id_id",
                table: "user_accounts",
                columns: new[] { "organization_id", "id" });

            migrationBuilder.CreateTable(
                name: "branches",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    normalized_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    contact_email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    address = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    municipality_code = table.Column<string>(type: "character(5)", fixedLength: true, maxLength: 5, nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_branches", x => x.id);
                    table.UniqueConstraint("ak_branches_organization_id_id", x => new { x.organization_id, x.id });
                    table.CheckConstraint("ck_branches_status", "status IN ('ACTIVE', 'INACTIVE')");
                    table.ForeignKey(
                        name: "fk_branches_municipalities_municipality_code",
                        column: x => x.municipality_code,
                        principalTable: "municipalities",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_branches_organizations_organization_id",
                        column: x => x.organization_id,
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "user_branch_accesses",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    granted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    granted_by_user_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    revoked_by_user_account_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_branch_accesses", x => x.id);
                    table.ForeignKey(
                        name: "fk_user_branch_accesses_branches_organization_id_branch_id",
                        columns: x => new { x.organization_id, x.branch_id },
                        principalTable: "branches",
                        principalColumns: new[] { "organization_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_user_branch_accesses_user_accounts_organization_id_granted_",
                        columns: x => new { x.organization_id, x.granted_by_user_account_id },
                        principalTable: "user_accounts",
                        principalColumns: new[] { "organization_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_user_branch_accesses_user_accounts_organization_id_revoked_",
                        columns: x => new { x.organization_id, x.revoked_by_user_account_id },
                        principalTable: "user_accounts",
                        principalColumns: new[] { "organization_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_user_branch_accesses_user_accounts_organization_id_user_acc",
                        columns: x => new { x.organization_id, x.user_account_id },
                        principalTable: "user_accounts",
                        principalColumns: new[] { "organization_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_security_audit_events_actor_user_account_id",
                table: "security_audit_events",
                column: "actor_user_account_id");

            migrationBuilder.CreateIndex(
                name: "ix_security_audit_events_branch_id",
                table: "security_audit_events",
                column: "branch_id");

            migrationBuilder.CreateIndex(
                name: "ix_branches_municipality_code",
                table: "branches",
                column: "municipality_code");

            migrationBuilder.CreateIndex(
                name: "ix_branches_organization_id_normalized_name",
                table: "branches",
                columns: new[] { "organization_id", "normalized_name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_branches_organization_id_status_name",
                table: "branches",
                columns: new[] { "organization_id", "status", "name" });

            migrationBuilder.CreateIndex(
                name: "ix_user_branch_accesses_organization_id_branch_id_revoked_at",
                table: "user_branch_accesses",
                columns: new[] { "organization_id", "branch_id", "revoked_at" });

            migrationBuilder.CreateIndex(
                name: "ix_user_branch_accesses_organization_id_granted_by_user_accoun",
                table: "user_branch_accesses",
                columns: new[] { "organization_id", "granted_by_user_account_id" });

            migrationBuilder.CreateIndex(
                name: "ix_user_branch_accesses_organization_id_revoked_by_user_accoun",
                table: "user_branch_accesses",
                columns: new[] { "organization_id", "revoked_by_user_account_id" });

            migrationBuilder.CreateIndex(
                name: "ix_user_branch_accesses_organization_id_user_account_id",
                table: "user_branch_accesses",
                columns: new[] { "organization_id", "user_account_id" });

            migrationBuilder.CreateIndex(
                name: "ix_user_branch_accesses_user_account_id_branch_id",
                table: "user_branch_accesses",
                columns: new[] { "user_account_id", "branch_id" },
                unique: true,
                filter: "revoked_at IS NULL");

            migrationBuilder.AddForeignKey(
                name: "fk_security_audit_events_branches_branch_id",
                table: "security_audit_events",
                column: "branch_id",
                principalTable: "branches",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_security_audit_events_user_accounts_actor_user_account_id",
                table: "security_audit_events",
                column: "actor_user_account_id",
                principalTable: "user_accounts",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_security_audit_events_branches_branch_id",
                table: "security_audit_events");

            migrationBuilder.DropForeignKey(
                name: "fk_security_audit_events_user_accounts_actor_user_account_id",
                table: "security_audit_events");

            migrationBuilder.DropTable(
                name: "user_branch_accesses");

            migrationBuilder.DropTable(
                name: "branches");

            migrationBuilder.DropUniqueConstraint(
                name: "ak_user_accounts_organization_id_id",
                table: "user_accounts");

            migrationBuilder.DropIndex(
                name: "ix_security_audit_events_actor_user_account_id",
                table: "security_audit_events");

            migrationBuilder.DropIndex(
                name: "ix_security_audit_events_branch_id",
                table: "security_audit_events");

            migrationBuilder.DropColumn(
                name: "actor_user_account_id",
                table: "security_audit_events");

            migrationBuilder.DropColumn(
                name: "branch_id",
                table: "security_audit_events");
        }
    }
}
