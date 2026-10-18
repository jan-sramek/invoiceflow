using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InvoiceFlow.Infrastructure.Persistence.Tenant.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentsAndIdempotency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "duplicate_of_invoice_id",
                table: "invoices",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "duplicate_rule",
                table: "invoices",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "duplicate_status",
                table: "invoices",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "None");

            migrationBuilder.CreateTable(
                name: "idempotency_keys",
                columns: table => new
                {
                    key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    user_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    request_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    invoice_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_idempotency_keys", x => new { x.key, x.user_id });
                });

            migrationBuilder.CreateTable(
                name: "invoice_documents",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    storage_key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    original_file_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    content_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    invoice_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_invoice_documents", x => x.id);
                    table.ForeignKey(
                        name: "fk_invoice_documents_invoices_invoice_id",
                        column: x => x.invoice_id,
                        principalTable: "invoices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_idempotency_keys_created_at",
                table: "idempotency_keys",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_invoice_documents_invoice_id",
                table: "invoice_documents",
                column: "invoice_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_invoice_documents_sha256",
                table: "invoice_documents",
                column: "sha256");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "idempotency_keys");

            migrationBuilder.DropTable(
                name: "invoice_documents");

            migrationBuilder.DropColumn(
                name: "duplicate_of_invoice_id",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "duplicate_rule",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "duplicate_status",
                table: "invoices");
        }
    }
}
