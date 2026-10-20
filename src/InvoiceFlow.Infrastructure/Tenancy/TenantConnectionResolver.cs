using InvoiceFlow.Application.Errors;
using InvoiceFlow.Domain.Tenants;
using InvoiceFlow.Infrastructure.Persistence.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Npgsql;

namespace InvoiceFlow.Infrastructure.Tenancy;

/// <summary>
/// Builds a tenant's connection string from its catalog entry and its file in the mounted Secret.
/// </summary>
internal sealed class TenantConnectionResolver(
    IDbContextFactory<CatalogDbContext> catalog,
    IMemoryCache cache,
    IOptions<TenancyOptions> options)
{
    /// <exception cref="TenantUnavailableException">Unknown or inactive tenant, or no readable credentials.</exception>
    /// <exception cref="TenantMaintenanceException">Schema behind, missing, or last migration failed.</exception>
    public async Task<string> GetConnectionStringAsync(TenantId tenantId, CancellationToken cancellationToken) =>
        (await cache.GetOrCreateAsync(
            (nameof(TenantConnectionResolver), tenantId),
            entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = options.Value.CacheDuration;
                return BuildAsync(tenantId, cancellationToken);
            }))!;

    private async Task<string> BuildAsync(TenantId tenantId, CancellationToken cancellationToken)
    {
        await using CatalogDbContext db = await catalog.CreateDbContextAsync(cancellationToken);
        TenantDatabase database = await db.Tenants
            .Where(t => t.Id == tenantId && t.Status == TenantStatus.Active)
            .Select(t => t.Database)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new TenantUnavailableException();

        EnsureSchemaReady(database);

        TenantCredentials credentials = await TenantCredentials.ReadAsync(
            options.Value.CredentialsPath, database.CredentialsKey, cancellationToken);

        NpgsqlConnectionStringBuilder connectionString = credentials.ConnectTo(
            database,
            options.Value.RootCertificatePath,
            options.Value.ConnectTimeoutSeconds);
        connectionString.MaxPoolSize = options.Value.MaxPoolSize;
        return connectionString.ConnectionString;
    }

    private static void EnsureSchemaReady(TenantDatabase database)
    {
        if (database.LastMigrationError is not null)
        {
            throw new TenantMaintenanceException();
        }

        if (database.SchemaVersion is null
            || string.CompareOrdinal(database.SchemaVersion, TenantSchemaRequirements.LatestMigrationId) < 0)
        {
            throw new TenantMaintenanceException();
        }
    }
}
