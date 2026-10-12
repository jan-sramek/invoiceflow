using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InvoiceFlow.Infrastructure.Persistence.Catalog.Migrations
{
    /// <inheritdoc />
    public partial class AddMigrationTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "last_migrated_at",
                table: "tenant_databases",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "last_migration_error",
                table: "tenant_databases",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "runtime_role",
                table: "tenant_databases",
                type: "character varying(63)",
                maxLength: 63,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "schema_version",
                table: "tenant_databases",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "last_migrated_at",
                table: "tenant_databases");

            migrationBuilder.DropColumn(
                name: "last_migration_error",
                table: "tenant_databases");

            migrationBuilder.DropColumn(
                name: "runtime_role",
                table: "tenant_databases");

            migrationBuilder.DropColumn(
                name: "schema_version",
                table: "tenant_databases");
        }
    }
}
