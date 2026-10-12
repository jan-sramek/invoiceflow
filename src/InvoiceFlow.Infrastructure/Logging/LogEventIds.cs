namespace InvoiceFlow.Infrastructure.Logging;

// Catalogue and id ranges: docs/log-events.md
internal static class LogEventIds
{
    public const int CatalogMigrated = 100;
    public const int TenantMigrated = 101;
    public const int TenantMigrationFailed = 102;
    public const int SchemaMigrationFinished = 103;
}
