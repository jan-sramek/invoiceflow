using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InvoiceFlow.Infrastructure.Persistence.Tenant.Migrations
{
    /// <inheritdoc />
    public partial class AddFieldStatusesAndDocumentLanguage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "currency_status",
                table: "invoices",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "document_language",
                table: "invoices",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "due_date_status",
                table: "invoices",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "invoice_date_status",
                table: "invoices",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "invoice_number_status",
                table: "invoices",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "purchase_order_reference_status",
                table: "invoices",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "subtotal_status",
                table: "invoices",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "supplier_name_status",
                table: "invoices",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "tax_amount_status",
                table: "invoices",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "total_amount_status",
                table: "invoices",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "currency_status",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "document_language",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "due_date_status",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "invoice_date_status",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "invoice_number_status",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "purchase_order_reference_status",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "subtotal_status",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "supplier_name_status",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "tax_amount_status",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "total_amount_status",
                table: "invoices");
        }
    }
}
