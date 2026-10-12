namespace InvoiceFlow.Infrastructure.Persistence.SchemaMigration;

public interface ISchemaMigrator
{
    /// <summary>
    /// Migrates the catalog, then every tenant database that is not still being provisioned.
    /// A catalog failure throws; a tenant failure is recorded in the catalog and counted.
    /// </summary>
    Task<SchemaMigrationResult> MigrateAsync(CancellationToken cancellationToken);
}

public sealed record SchemaMigrationResult(int Succeeded, int Failed);
