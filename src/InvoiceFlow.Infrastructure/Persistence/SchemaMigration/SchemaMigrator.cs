using InvoiceFlow.Application.Audit;
using InvoiceFlow.Domain.Tenants;
using InvoiceFlow.Infrastructure.Logging;
using InvoiceFlow.Infrastructure.Persistence.Catalog;
using InvoiceFlow.Infrastructure.Persistence.Tenant;
using InvoiceFlow.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

namespace InvoiceFlow.Infrastructure.Persistence.SchemaMigration;

internal sealed partial class SchemaMigrator(
    IDbContextFactory<CatalogDbContext> catalogFactory,
    IOptions<SchemaMigrationOptions> options,
    TimeProvider timeProvider,
    ILoggerFactory loggerFactory,
    ILogger<SchemaMigrator> logger) : ISchemaMigrator
{
    public async Task<SchemaMigrationResult> MigrateAsync(CancellationToken cancellationToken)
    {
        await using CatalogDbContext catalog = await catalogFactory.CreateDbContextAsync(cancellationToken);
        await catalog.Database.MigrateAsync(cancellationToken);
        string catalogVersion = (await catalog.Database.GetAppliedMigrationsAsync(cancellationToken)).Last();
        LogCatalogMigrated(logger, catalogVersion);

        List<TenantDatabase> databases = await catalog.TenantDatabases
            .Where(d => catalog.Tenants.Any(t => t.Id == d.TenantId && t.Status != TenantStatus.Provisioning))
            .OrderBy(d => d.TenantId)
            .ToListAsync(cancellationToken);

        int failed = 0;
        foreach (TenantDatabase database in databases)
        {
            try
            {
                database.SchemaVersion = await MigrateTenantAsync(database, cancellationToken);
                database.LastMigratedAt = timeProvider.GetUtcNow();
                database.LastMigrationError = null;
                LogTenantMigrated(logger, database.TenantId.Value, database.SchemaVersion);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                failed++;
                string error = exception.GetBaseException().Message;
                database.LastMigrationError = error.Length > TenantDatabase.MaxErrorLength
                    ? error[..TenantDatabase.MaxErrorLength]
                    : error;
                LogTenantMigrationFailed(logger, exception, database.TenantId.Value);
            }

            await catalog.SaveChangesAsync(cancellationToken);
        }

        var result = new SchemaMigrationResult(databases.Count - failed, failed);
        LogSchemaMigrationFinished(
            logger, failed > 0 ? LogLevel.Warning : LogLevel.Information, result.Succeeded, result.Failed);
        return result;
    }

    private async Task<string> MigrateTenantAsync(TenantDatabase database, CancellationToken cancellationToken)
    {
        TenantCredentials credentials = await TenantCredentials.ReadAsync(
            options.Value.CredentialsPath, database.CredentialsKey, cancellationToken);

        NpgsqlConnectionStringBuilder connectionString = credentials.ConnectTo(
            database,
            options.Value.RootCertificatePath);
        connectionString.Pooling = false;

        DbContextOptionsBuilder<TenantDbContext> contextOptions =
            new DbContextOptionsBuilder<TenantDbContext>().UseLoggerFactory(loggerFactory);
        TenantDbContext.Configure(contextOptions, connectionString.ConnectionString);

        await using var tenant = new TenantDbContext(contextOptions.Options, new FixedTenantContext(database.TenantId));
        await tenant.Database.OpenConnectionAsync(cancellationToken);
        await tenant.Database.MigrateAsync(cancellationToken);
        await ClaimAsync(tenant, database, cancellationToken);
        await GrantRuntimeAccessAsync(tenant, database.RuntimeRole, cancellationToken);

        return (await tenant.Database.GetAppliedMigrationsAsync(cancellationToken)).Last();
    }

    // A fresh database is stamped with its tenant. One that belongs to another tenant gets no grants,
    // so a wrong catalog entry can't hand its data to this tenant's runtime role.
    private static async Task ClaimAsync(TenantDbContext tenant, TenantDatabase database, CancellationToken cancellationToken)
    {
        List<TenantId> owners = await tenant.Settings.Select(s => s.TenantId).ToListAsync(cancellationToken);

        if (owners.Count == 0)
        {
            tenant.Settings.Add(new TenantSettings { TenantId = database.TenantId });
            await tenant.SaveChangesAsync(cancellationToken);
        }
        else if (owners is not [var owner] || owner != database.TenantId)
        {
            throw new InvalidOperationException(
                $"Database '{database.DatabaseName}' does not belong to tenant {database.TenantId}.");
        }
    }

    // One transaction, so the runtime role never holds UPDATE on the audit trail, even briefly.
    private static async Task GrantRuntimeAccessAsync(
        TenantDbContext tenant,
        string runtimeRole,
        CancellationToken cancellationToken)
    {
        string role = QuoteIdentifier(runtimeRole);
        string history = QuoteIdentifier(HistoryRepository.DefaultTableName);
        string settings = TableOf<TenantSettings>(tenant);
        string audit = TableOf<AuditEntry>(tenant);

        string sql = $"""
            GRANT USAGE ON SCHEMA public TO {role};
            GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO {role};
            GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public TO {role};
            REVOKE INSERT, UPDATE, DELETE ON {history} FROM {role};
            REVOKE INSERT, UPDATE, DELETE ON {settings} FROM {role};
            REVOKE UPDATE, DELETE ON {audit} FROM {role};
            """;

        await using IDbContextTransaction transaction = await tenant.Database.BeginTransactionAsync(cancellationToken);
        await tenant.Database.ExecuteSqlRawAsync(sql, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private static string TableOf<TEntity>(TenantDbContext tenant) =>
        QuoteIdentifier(tenant.Model.FindEntityType(typeof(TEntity))?.GetTableName()
            ?? throw new InvalidOperationException($"{typeof(TEntity).Name} is not mapped to a table."));

    private static string QuoteIdentifier(string name) =>
        $"\"{name.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";

    [LoggerMessage(
        EventId = LogEventIds.CatalogMigrated,
        EventName = "CatalogMigrated",
        Level = LogLevel.Information,
        Message = "Catalog migrated to {SchemaVersion}")]
    private static partial void LogCatalogMigrated(ILogger logger, string schemaVersion);

    [LoggerMessage(
        EventId = LogEventIds.TenantMigrated,
        EventName = "TenantMigrated",
        Level = LogLevel.Information,
        Message = "Tenant {TenantId} migrated to {SchemaVersion}")]
    private static partial void LogTenantMigrated(ILogger logger, Guid tenantId, string schemaVersion);

    [LoggerMessage(
        EventId = LogEventIds.TenantMigrationFailed,
        EventName = "TenantMigrationFailed",
        Level = LogLevel.Error,
        Message = "Migrating tenant {TenantId} failed")]
    private static partial void LogTenantMigrationFailed(ILogger logger, Exception exception, Guid tenantId);

    [LoggerMessage(
        EventId = LogEventIds.SchemaMigrationFinished,
        EventName = "SchemaMigrationFinished",
        Message = "Schema migration finished: {Succeeded} tenant databases migrated, {Failed} failed")]
    private static partial void LogSchemaMigrationFinished(ILogger logger, LogLevel level, int succeeded, int failed);
}
