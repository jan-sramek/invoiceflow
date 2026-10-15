using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InvoiceFlow.Infrastructure.Persistence.Tenant.Migrations
{
    /// <inheritdoc />
    public partial class AddInvoiceListIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:pg_trgm", ",,");

            // EF Core cannot index properties of a complex type, so these are not in the model.
            migrationBuilder.Sql(
                """
                CREATE INDEX ix_invoices_status_due_date ON invoices (status, due_date);
                CREATE INDEX ix_invoices_supplier_name_trgm ON invoices USING gin (supplier_name gin_trgm_ops);
                CREATE INDEX ix_invoices_invoice_number_trgm ON invoices USING gin (invoice_number gin_trgm_ops);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DROP INDEX ix_invoices_invoice_number_trgm;
                DROP INDEX ix_invoices_supplier_name_trgm;
                DROP INDEX ix_invoices_status_due_date;
                """);

            migrationBuilder.AlterDatabase()
                .OldAnnotation("Npgsql:PostgresExtension:pg_trgm", ",,");
        }
    }
}
