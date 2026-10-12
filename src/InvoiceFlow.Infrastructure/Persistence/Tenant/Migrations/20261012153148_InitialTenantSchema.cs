using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InvoiceFlow.Infrastructure.Persistence.Tenant.Migrations
{
    /// <inheritdoc />
    public partial class InitialTenantSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "audit_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    action = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    subject_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    subject_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    correlation_id = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    actor_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    actor_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    actor_type = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    changes = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_audit_events", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "invoices",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    uploaded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    paid_on = table.Column<DateOnly>(type: "date", nullable: true),
                    payment_reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    due_date = table.Column<DateOnly>(type: "date", nullable: true),
                    invoice_date = table.Column<DateOnly>(type: "date", nullable: true),
                    invoice_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    purchase_order_reference = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    subtotal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    supplier_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    tax_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    total_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    submitted_by_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    submitted_by_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    submitted_by_type = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    uploaded_by_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    uploaded_by_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    uploaded_by_type = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_invoices", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_audit_events_occurred_at",
                table: "audit_events",
                column: "occurred_at");

            migrationBuilder.CreateIndex(
                name: "ix_audit_events_subject_type_subject_id_occurred_at",
                table: "audit_events",
                columns: new[] { "subject_type", "subject_id", "occurred_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "audit_events");

            migrationBuilder.DropTable(
                name: "invoices");
        }
    }
}
