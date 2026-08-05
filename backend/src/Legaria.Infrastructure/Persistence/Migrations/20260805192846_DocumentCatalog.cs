using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Legaria.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DocumentCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "document_categories",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    normalized_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    scope = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_document_categories", x => x.id);
                    table.UniqueConstraint("ak_document_categories_organization_id_id", x => new { x.organization_id, x.id });
                    table.CheckConstraint("ck_document_categories_scope", "scope IN ('EMPLOYEE', 'BRANCH')");
                    table.CheckConstraint("ck_document_categories_status", "status IN ('ACTIVE', 'INACTIVE')");
                    table.ForeignKey(
                        name: "fk_document_categories_organizations_organization_id",
                        column: x => x.organization_id,
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "document_types",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    category_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    normalized_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    status = table.Column<string>(type: "text", nullable: false),
                    is_required_by_default = table.Column<bool>(type: "boolean", nullable: false),
                    issue_date_mode = table.Column<string>(type: "text", nullable: false),
                    expiration_date_mode = table.Column<string>(type: "text", nullable: false),
                    allows_multiple_active_versions = table.Column<bool>(type: "boolean", nullable: false),
                    allows_multiple_evidence_items = table.Column<bool>(type: "boolean", nullable: false),
                    allowed_evidence_kinds = table.Column<string[]>(type: "text[]", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_document_types", x => x.id);
                    table.UniqueConstraint("ak_document_types_organization_id_id", x => new { x.organization_id, x.id });
                    table.CheckConstraint("ck_document_types_evidence_kinds", "cardinality(allowed_evidence_kinds) > 0 AND allowed_evidence_kinds <@ ARRAY['PDF', 'IMAGE', 'VIDEO', 'LINK']::text[]");
                    table.CheckConstraint("ck_document_types_expiration_date_mode", "expiration_date_mode IN ('NEVER', 'OPTIONAL', 'REQUIRED')");
                    table.CheckConstraint("ck_document_types_issue_date_mode", "issue_date_mode IN ('NEVER', 'OPTIONAL', 'REQUIRED')");
                    table.CheckConstraint("ck_document_types_status", "status IN ('ACTIVE', 'INACTIVE')");
                    table.ForeignKey(
                        name: "fk_document_types_document_categories_organization_id_category",
                        columns: x => new { x.organization_id, x.category_id },
                        principalTable: "document_categories",
                        principalColumns: new[] { "organization_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_document_categories_organization_id_scope_normalized_name",
                table: "document_categories",
                columns: new[] { "organization_id", "scope", "normalized_name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_document_types_organization_id_category_id_normalized_name",
                table: "document_types",
                columns: new[] { "organization_id", "category_id", "normalized_name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "document_types");

            migrationBuilder.DropTable(
                name: "document_categories");
        }
    }
}
