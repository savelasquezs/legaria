using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Legaria.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EmployeesEmploymentAndIntegratedBranchAdministration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM user_accounts AS account
                        INNER JOIN user_roles AS role ON role.user_account_id = account.id
                        WHERE role.system_role_id = 'ca3759ba-98b6-4de0-b3a7-44ef0f274e87'
                          AND account.employee_id IS NULL
                    ) THEN
                        RAISE EXCEPTION 'Existen cuentas BRANCH_ADMIN sin employee_id. Vincúlelas manualmente a un trabajador antes de aplicar EmployeesEmploymentAndIntegratedBranchAdministration.';
                    END IF;
                END $$;
                """);

            migrationBuilder.DropIndex(
                name: "ix_user_accounts_organization_id_employee_id",
                table: "user_accounts");

            migrationBuilder.CreateTable(
                name: "employment_relationships",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    employee_id = table.Column<Guid>(type: "uuid", nullable: false),
                    started_on = table.Column<DateOnly>(type: "date", nullable: false),
                    ended_on = table.Column<DateOnly>(type: "date", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_employment_relationships", x => x.id);
                    table.UniqueConstraint("ak_employment_relationships_organization_id_id", x => new { x.organization_id, x.id });
                    table.CheckConstraint("ck_employment_relationships_dates", "ended_on IS NULL OR ended_on >= started_on");
                    table.ForeignKey(
                        name: "fk_employment_relationships_employees_organization_id_employee",
                        columns: x => new { x.organization_id, x.employee_id },
                        principalTable: "employees",
                        principalColumns: new[] { "organization_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "job_positions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    normalized_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_job_positions", x => x.id);
                    table.UniqueConstraint("ak_job_positions_organization_id_id", x => new { x.organization_id, x.id });
                    table.CheckConstraint("ck_job_positions_status", "status IN ('ACTIVE', 'INACTIVE')");
                    table.ForeignKey(
                        name: "fk_job_positions_organizations_organization_id",
                        column: x => x.organization_id,
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "employee_assignments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    employment_relationship_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    job_position_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_primary = table.Column<bool>(type: "boolean", nullable: false),
                    started_on = table.Column<DateOnly>(type: "date", nullable: false),
                    ended_on = table.Column<DateOnly>(type: "date", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_employee_assignments", x => x.id);
                    table.CheckConstraint("ck_employee_assignments_dates", "ended_on IS NULL OR ended_on >= started_on");
                    table.ForeignKey(
                        name: "fk_employee_assignments_branches_organization_id_branch_id",
                        columns: x => new { x.organization_id, x.branch_id },
                        principalTable: "branches",
                        principalColumns: new[] { "organization_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_employee_assignments_employment_relationships_organization_",
                        columns: x => new { x.organization_id, x.employment_relationship_id },
                        principalTable: "employment_relationships",
                        principalColumns: new[] { "organization_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_employee_assignments_job_positions_organization_id_job_posi",
                        columns: x => new { x.organization_id, x.job_position_id },
                        principalTable: "job_positions",
                        principalColumns: new[] { "organization_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_user_accounts_organization_id_employee_id",
                table: "user_accounts",
                columns: new[] { "organization_id", "employee_id" },
                unique: true,
                filter: "\"employee_id\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_employee_assignments_organization_id_branch_id_ended_on",
                table: "employee_assignments",
                columns: new[] { "organization_id", "branch_id", "ended_on" });

            migrationBuilder.CreateIndex(
                name: "ix_employee_assignments_organization_id_employment_relationshi",
                table: "employee_assignments",
                columns: new[] { "organization_id", "employment_relationship_id" },
                unique: true,
                filter: "\"is_primary\" = TRUE AND \"ended_on\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_employee_assignments_organization_id_job_position_id",
                table: "employee_assignments",
                columns: new[] { "organization_id", "job_position_id" });

            migrationBuilder.CreateIndex(
                name: "ix_employment_relationships_organization_id_employee_id_starte",
                table: "employment_relationships",
                columns: new[] { "organization_id", "employee_id", "started_on" });

            migrationBuilder.CreateIndex(
                name: "ix_job_positions_organization_id_normalized_name",
                table: "job_positions",
                columns: new[] { "organization_id", "normalized_name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "employee_assignments");

            migrationBuilder.DropTable(
                name: "employment_relationships");

            migrationBuilder.DropTable(
                name: "job_positions");

            migrationBuilder.DropIndex(
                name: "ix_user_accounts_organization_id_employee_id",
                table: "user_accounts");

            migrationBuilder.CreateIndex(
                name: "ix_user_accounts_organization_id_employee_id",
                table: "user_accounts",
                columns: new[] { "organization_id", "employee_id" });
        }
    }
}
