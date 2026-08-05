using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Legaria.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EmployeeDocuments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "employee_documents",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    employee_id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_type_id = table.Column<Guid>(type: "uuid", nullable: false),
                    issued_on = table.Column<DateOnly>(type: "date", nullable: true),
                    expires_on = table.Column<DateOnly>(type: "date", nullable: true),
                    replaced_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    uploaded_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_employee_documents", x => x.id);
                    table.UniqueConstraint("ak_employee_documents_organization_id_id", x => new { x.organization_id, x.id });
                    table.CheckConstraint("ck_employee_documents_dates", "expires_on IS NULL OR issued_on IS NULL OR expires_on >= issued_on");
                    table.ForeignKey(
                        name: "fk_employee_documents_document_types_organization_id_document_",
                        columns: x => new { x.organization_id, x.document_type_id },
                        principalTable: "document_types",
                        principalColumns: new[] { "organization_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_employee_documents_employees_organization_id_employee_id",
                        columns: x => new { x.organization_id, x.employee_id },
                        principalTable: "employees",
                        principalColumns: new[] { "organization_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_employee_documents_user_accounts_organization_id_uploaded_b",
                        columns: x => new { x.organization_id, x.uploaded_by_user_id },
                        principalTable: "user_accounts",
                        principalColumns: new[] { "organization_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "employee_document_evidences",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    employee_document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    storage_key = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    original_file_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    content_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    size_bytes = table.Column<long>(type: "bigint", nullable: true),
                    url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_employee_document_evidences", x => x.id);
                    table.CheckConstraint("ck_employee_document_evidences_kind", "kind IN ('PDF', 'IMAGE', 'VIDEO', 'LINK')");
                    table.CheckConstraint("ck_employee_document_evidences_payload", "(kind = 'LINK' AND url IS NOT NULL AND storage_key IS NULL) OR (kind <> 'LINK' AND url IS NULL AND storage_key IS NOT NULL)");
                    table.ForeignKey(
                        name: "fk_employee_document_evidences_employee_documents_organization",
                        columns: x => new { x.organization_id, x.employee_document_id },
                        principalTable: "employee_documents",
                        principalColumns: new[] { "organization_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_employee_document_evidences_organization_id_employee_docume",
                table: "employee_document_evidences",
                columns: new[] { "organization_id", "employee_document_id" });

            migrationBuilder.CreateIndex(
                name: "ix_employee_documents_organization_id_document_type_id",
                table: "employee_documents",
                columns: new[] { "organization_id", "document_type_id" });

            migrationBuilder.CreateIndex(
                name: "ix_employee_documents_organization_id_employee_id_document_typ",
                table: "employee_documents",
                columns: new[] { "organization_id", "employee_id", "document_type_id", "replaced_at" });

            migrationBuilder.CreateIndex(
                name: "ix_employee_documents_organization_id_uploaded_by_user_id",
                table: "employee_documents",
                columns: new[] { "organization_id", "uploaded_by_user_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "employee_document_evidences");

            migrationBuilder.DropTable(
                name: "employee_documents");
        }
    }
}
