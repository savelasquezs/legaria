using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Legaria.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class OrganizationProvisioningAndDivipola : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM organizations) THEN
                        RAISE EXCEPTION USING
                            MESSAGE = 'OrganizationProvisioningAndDivipola no puede inventar NIT ni municipio para organizaciones existentes.',
                            HINT = 'Respalde la base, complete o migre manualmente los datos empresariales y vuelva a ejecutar la migración.';
                    END IF;

                    IF EXISTS (
                        SELECT normalized_email
                        FROM (
                            SELECT normalized_email FROM platform_users
                            UNION ALL
                            SELECT normalized_email FROM user_accounts
                        ) account_email_candidates
                        GROUP BY normalized_email
                        HAVING COUNT(*) > 1
                    ) THEN
                        RAISE EXCEPTION USING
                            MESSAGE = 'Existen correos de cuenta duplicados entre plataforma y tenant.',
                            HINT = 'Corrija los correos duplicados antes de volver a ejecutar la migración.';
                    END IF;
                END $$;
                """);

            migrationBuilder.RenameColumn(
                name: "name",
                table: "organizations",
                newName: "trade_name");

            migrationBuilder.AddColumn<bool>(
                name: "is_initial_administrator",
                table: "user_accounts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "organization_id",
                table: "security_audit_events",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "address",
                table: "organizations",
                type: "character varying(250)",
                maxLength: 250,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "contact_email",
                table: "organizations",
                type: "character varying(320)",
                maxLength: 320,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "legal_name",
                table: "organizations",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "municipality_code",
                table: "organizations",
                type: "character(5)",
                fixedLength: true,
                maxLength: 5,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "nit",
                table: "organizations",
                type: "character varying(14)",
                maxLength: 14,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "phone",
                table: "organizations",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "verification_digit",
                table: "organizations",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "delivered_at",
                table: "account_tokens",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "delivery_failed_at",
                table: "account_tokens",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "account_emails",
                columns: table => new
                {
                    normalized_email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    account_type = table.Column<string>(type: "text", nullable: false),
                    platform_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    user_account_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_account_emails", x => x.normalized_email);
                    table.CheckConstraint("ck_account_emails_account_type", "(account_type = 'PLATFORM' AND platform_user_id IS NOT NULL AND user_account_id IS NULL) OR (account_type = 'TENANT' AND user_account_id IS NOT NULL AND platform_user_id IS NULL)");
                    table.CheckConstraint("ck_account_emails_single_account", "num_nonnulls(platform_user_id, user_account_id) = 1");
                    table.ForeignKey(
                        name: "fk_account_emails_platform_users_platform_user_id",
                        column: x => x.platform_user_id,
                        principalTable: "platform_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_account_emails_user_accounts_user_account_id",
                        column: x => x.user_account_id,
                        principalTable: "user_accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "departments",
                columns: table => new
                {
                    code = table.Column<string>(type: "character(2)", fixedLength: true, maxLength: 2, nullable: false),
                    name = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_departments", x => x.code);
                });

            migrationBuilder.CreateTable(
                name: "municipalities",
                columns: table => new
                {
                    code = table.Column<string>(type: "character(5)", fixedLength: true, maxLength: 5, nullable: false),
                    department_code = table.Column<string>(type: "character(2)", fixedLength: true, maxLength: 2, nullable: false),
                    name = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_municipalities", x => x.code);
                    table.ForeignKey(
                        name: "fk_municipalities_departments_department_code",
                        column: x => x.department_code,
                        principalTable: "departments",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.Sql(LoadEmbeddedSql("DivipolaMgn2025.sql"));

            migrationBuilder.Sql(
                """
                INSERT INTO account_emails
                    (normalized_email, account_type, platform_user_id, user_account_id, created_at)
                SELECT normalized_email, 'PLATFORM', id, NULL, created_at
                FROM platform_users;

                INSERT INTO account_emails
                    (normalized_email, account_type, platform_user_id, user_account_id, created_at)
                SELECT normalized_email, 'TENANT', NULL, id, created_at
                FROM user_accounts;
                """);

            migrationBuilder.CreateIndex(
                name: "ix_user_accounts_organization_id",
                table: "user_accounts",
                column: "organization_id",
                unique: true,
                filter: "\"is_initial_administrator\" = TRUE");

            migrationBuilder.CreateIndex(
                name: "ix_security_audit_events_organization_id",
                table: "security_audit_events",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "ix_organizations_municipality_code",
                table: "organizations",
                column: "municipality_code");

            migrationBuilder.CreateIndex(
                name: "ix_organizations_nit",
                table: "organizations",
                column: "nit",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_account_emails_platform_user_id",
                table: "account_emails",
                column: "platform_user_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_account_emails_user_account_id",
                table: "account_emails",
                column: "user_account_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_municipalities_department_code_name",
                table: "municipalities",
                columns: new[] { "department_code", "name" });

            migrationBuilder.AddForeignKey(
                name: "fk_organizations_municipalities_municipality_code",
                table: "organizations",
                column: "municipality_code",
                principalTable: "municipalities",
                principalColumn: "code",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_security_audit_events_organizations_organization_id",
                table: "security_audit_events",
                column: "organization_id",
                principalTable: "organizations",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_organizations_municipalities_municipality_code",
                table: "organizations");

            migrationBuilder.DropForeignKey(
                name: "fk_security_audit_events_organizations_organization_id",
                table: "security_audit_events");

            migrationBuilder.DropTable(
                name: "account_emails");

            migrationBuilder.DropTable(
                name: "municipalities");

            migrationBuilder.DropTable(
                name: "departments");

            migrationBuilder.DropIndex(
                name: "ix_user_accounts_organization_id",
                table: "user_accounts");

            migrationBuilder.DropIndex(
                name: "ix_security_audit_events_organization_id",
                table: "security_audit_events");

            migrationBuilder.DropIndex(
                name: "ix_organizations_municipality_code",
                table: "organizations");

            migrationBuilder.DropIndex(
                name: "ix_organizations_nit",
                table: "organizations");

            migrationBuilder.DropColumn(
                name: "is_initial_administrator",
                table: "user_accounts");

            migrationBuilder.DropColumn(
                name: "organization_id",
                table: "security_audit_events");

            migrationBuilder.DropColumn(
                name: "address",
                table: "organizations");

            migrationBuilder.DropColumn(
                name: "contact_email",
                table: "organizations");

            migrationBuilder.DropColumn(
                name: "legal_name",
                table: "organizations");

            migrationBuilder.DropColumn(
                name: "municipality_code",
                table: "organizations");

            migrationBuilder.DropColumn(
                name: "nit",
                table: "organizations");

            migrationBuilder.DropColumn(
                name: "phone",
                table: "organizations");

            migrationBuilder.DropColumn(
                name: "verification_digit",
                table: "organizations");

            migrationBuilder.DropColumn(
                name: "delivered_at",
                table: "account_tokens");

            migrationBuilder.DropColumn(
                name: "delivery_failed_at",
                table: "account_tokens");

            migrationBuilder.RenameColumn(
                name: "trade_name",
                table: "organizations",
                newName: "name");
        }

        private static string LoadEmbeddedSql(string fileName)
        {
            var assembly = typeof(OrganizationProvisioningAndDivipola).Assembly;
            var resourceName = assembly.GetManifestResourceNames()
                .Single(name => name.EndsWith(fileName, StringComparison.Ordinal));
            using var stream = assembly.GetManifestResourceStream(resourceName)
                ?? throw new InvalidOperationException($"No se encontró el recurso {fileName}.");
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }
    }
}
