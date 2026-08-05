using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Legaria.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EmploymentLifecycleConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_employment_relationships_active_employee",
                table: "employment_relationships",
                columns: new[] { "organization_id", "employee_id" },
                unique: true,
                filter: "\"ended_on\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_employee_assignments_active_branch",
                table: "employee_assignments",
                columns: new[] { "organization_id", "employment_relationship_id", "branch_id" },
                unique: true,
                filter: "\"ended_on\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_employment_relationships_active_employee",
                table: "employment_relationships");

            migrationBuilder.DropIndex(
                name: "ix_employee_assignments_active_branch",
                table: "employee_assignments");
        }
    }
}
