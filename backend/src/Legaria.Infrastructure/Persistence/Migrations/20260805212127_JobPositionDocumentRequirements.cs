using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Legaria.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class JobPositionDocumentRequirements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "job_position_document_requirements",
                columns: table => new
                {
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    job_position_id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_type_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_job_position_document_requirements", x => new { x.organization_id, x.job_position_id, x.document_type_id });
                    table.ForeignKey(
                        name: "fk_job_position_document_requirements_document_types_organizat",
                        columns: x => new { x.organization_id, x.document_type_id },
                        principalTable: "document_types",
                        principalColumns: new[] { "organization_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_job_position_document_requirements_job_positions_organizati",
                        columns: x => new { x.organization_id, x.job_position_id },
                        principalTable: "job_positions",
                        principalColumns: new[] { "organization_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_job_position_document_requirements_organization_id_document",
                table: "job_position_document_requirements",
                columns: new[] { "organization_id", "document_type_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "job_position_document_requirements");
        }
    }
}
