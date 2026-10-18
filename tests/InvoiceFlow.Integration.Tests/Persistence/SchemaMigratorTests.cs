using InvoiceFlow.Domain.Tenants;
using InvoiceFlow.Infrastructure;
using InvoiceFlow.Infrastructure.Persistence.Catalog;
using InvoiceFlow.Infrastructure.Persistence.SchemaMigration;
using InvoiceFlow.Infrastructure.Persistence.Tenant;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace InvoiceFlow.Integration.Tests.Persistence;

/// <summary>
/// Each test gets its own catalog, so the migrator only sees the tenants registered by that test.
/// </summary>
public sealed class SchemaMigratorTests(PostgresFixture database) : IAsyncLifetime
{
    private readonly string _catalog = $"catalog_{Guid.NewGuid():N}";
    private readonly DirectoryInfo _credentials = Directory.CreateTempSubdirectory("invoiceflow-migrations-");
    private ServiceProvider? _services;

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        await database.ExecuteAsync("postgres", $"CREATE DATABASE {_catalog}");

        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"ConnectionStrings:{InfrastructureServiceCollectionExtensions.CatalogConnectionStringName}"] =
                    database.ConnectionString(_catalog),
                [$"{SchemaMigrationOptions.SectionName}:{nameof(SchemaMigrationOptions.CredentialsPath)}"] =
                    _credentials.FullName,
            })
            .Build();

        _services = new ServiceCollection()
            .AddLogging()
            .AddSchemaMigrator(configuration)
            .BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });

        // Creates the catalog schema, so tests can register tenants.
        await MigrateAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_services is not null)
        {
            await _services.DisposeAsync();
        }

        _credentials.Delete(recursive: true);
    }

    [Fact]
    public async Task Migrate_FreshTenantDatabase_AppliesSchemaAndClaimsIt()
    {
        Registered tenant = await RegisterTenantAsync();

        SchemaMigrationResult result = await MigrateAsync();

        result.ShouldBe(new SchemaMigrationResult(Succeeded: 1, Failed: 0));
        TenantDatabase entry = await CatalogEntryAsync(tenant.Id);
        entry.SchemaVersion.ShouldBe(LatestTenantMigration());
        entry.LastMigratedAt.ShouldNotBeNull();
        entry.LastMigrationError.ShouldBeNull();
        (await OwnersAsync(tenant.Database)).ShouldBe([tenant.Id.Value]);
    }

    [Fact]
    public async Task Migrate_Again_ChangesNothing()
    {
        Registered tenant = await RegisterTenantAsync();
        await MigrateAsync();

        SchemaMigrationResult result = await MigrateAsync();

        result.ShouldBe(new SchemaMigrationResult(Succeeded: 1, Failed: 0));
        (await CatalogEntryAsync(tenant.Id)).SchemaVersion.ShouldBe(LatestTenantMigration());
        (await OwnersAsync(tenant.Database)).ShouldBe([tenant.Id.Value]);
    }

    [Fact]
    public async Task Migrate_RuntimeRole_CanUseDataButNotRewriteAuditOrOwnership()
    {
        Registered tenant = await RegisterTenantAsync();

        await MigrateAsync();

        (await GrantedAsync(tenant.RuntimeRole, tenant.Database)).ShouldBe(
            [
                "__EFMigrationsHistory SELECT",
                "audit_events INSERT",
                "audit_events SELECT",
                "idempotency_keys DELETE",
                "idempotency_keys INSERT",
                "idempotency_keys SELECT",
                "idempotency_keys UPDATE",
                "invoice_documents DELETE",
                "invoice_documents INSERT",
                "invoice_documents SELECT",
                "invoice_documents UPDATE",
                "invoices DELETE",
                "invoices INSERT",
                "invoices SELECT",
                "invoices UPDATE",
                "tenant_settings SELECT",
            ],
            ignoreOrder: true);
    }

    [Fact]
    public async Task Migrate_OneTenantFails_OthersAreStillMigrated()
    {
        Registered broken = await RegisterTenantAsync(withCredentials: false);
        Registered healthy = await RegisterTenantAsync();

        SchemaMigrationResult result = await MigrateAsync();

        result.ShouldBe(new SchemaMigrationResult(Succeeded: 1, Failed: 1));
        TenantDatabase brokenEntry = await CatalogEntryAsync(broken.Id);
        brokenEntry.SchemaVersion.ShouldBeNull();
        brokenEntry.LastMigrationError.ShouldNotBeNullOrEmpty();
        (await CatalogEntryAsync(healthy.Id)).SchemaVersion.ShouldBe(LatestTenantMigration());
    }

    [Fact]
    public async Task Migrate_FailedTenantFixed_ClearsTheError()
    {
        Registered tenant = await RegisterTenantAsync(withCredentials: false);
        await MigrateAsync();
        await database.WriteCredentialsAsync(_credentials.FullName, tenant.Database);

        SchemaMigrationResult result = await MigrateAsync();

        result.ShouldBe(new SchemaMigrationResult(Succeeded: 1, Failed: 0));
        TenantDatabase entry = await CatalogEntryAsync(tenant.Id);
        entry.SchemaVersion.ShouldBe(LatestTenantMigration());
        entry.LastMigrationError.ShouldBeNull();
    }

    [Fact]
    public async Task Migrate_DatabaseOfAnotherTenant_GrantsNothing()
    {
        Registered owner = await RegisterTenantAsync();
        await MigrateAsync();
        Registered intruder = await RegisterTenantAsync(sharing: owner);

        SchemaMigrationResult result = await MigrateAsync();

        result.ShouldBe(new SchemaMigrationResult(Succeeded: 1, Failed: 1));
        (await CatalogEntryAsync(intruder.Id)).LastMigrationError.ShouldBe(
            $"Database '{owner.Database}' does not belong to tenant {intruder.Id}.");
        (await GrantedAsync(intruder.RuntimeRole, owner.Database)).ShouldBeEmpty();
        (await OwnersAsync(owner.Database)).ShouldBe([owner.Id.Value]);
    }

    [Fact]
    public async Task Migrate_TenantStillProvisioning_IsLeftAlone()
    {
        Registered tenant = await RegisterTenantAsync(status: TenantStatus.Provisioning);

        SchemaMigrationResult result = await MigrateAsync();

        result.ShouldBe(new SchemaMigrationResult(Succeeded: 0, Failed: 0));
        (await CatalogEntryAsync(tenant.Id)).SchemaVersion.ShouldBeNull();
    }

    private Task<SchemaMigrationResult> MigrateAsync() =>
        _services!.GetRequiredService<ISchemaMigrator>().MigrateAsync(CancellationToken);

    private async Task<Registered> RegisterTenantAsync(
        Registered? sharing = null,
        bool withCredentials = true,
        TenantStatus status = TenantStatus.Active)
    {
        var tenantId = new TenantId(Guid.CreateVersion7());
        string databaseName = sharing?.Database ?? $"tenant_{tenantId.Value:N}";
        string runtimeRole = $"runtime_{tenantId.Value:N}";

        if (sharing is null)
        {
            await database.ExecuteAsync("postgres", $"CREATE DATABASE {databaseName}");
        }

        await database.ExecuteAsync("postgres", $"CREATE ROLE {runtimeRole} NOLOGIN");

        if (withCredentials)
        {
            await database.WriteCredentialsAsync(_credentials.FullName, databaseName);
        }

        CatalogTenant tenant = database.CatalogEntry(tenantId, databaseName, runtimeRole);
        tenant.Status = status;

        await using CatalogDbContext catalog = OpenCatalog();
        catalog.Tenants.Add(tenant);
        await catalog.SaveChangesAsync(CancellationToken);

        return new Registered(tenantId, databaseName, runtimeRole);
    }

    private async Task<TenantDatabase> CatalogEntryAsync(TenantId tenantId)
    {
        await using CatalogDbContext catalog = OpenCatalog();
        return await catalog.TenantDatabases.AsNoTracking().SingleAsync(d => d.TenantId == tenantId, CancellationToken);
    }

    private CatalogDbContext OpenCatalog()
    {
        var options = new DbContextOptionsBuilder<CatalogDbContext>();
        CatalogDbContext.Configure(options, database.ConnectionString(_catalog));
        return new CatalogDbContext(options.Options);
    }

    private string LatestTenantMigration()
    {
        using TenantDbContext context = database.OpenUnrouted("postgres", default);
        return context.Database.GetMigrations().Last();
    }

    private async Task<List<Guid>> OwnersAsync(string databaseName)
    {
        await using var dataSource = NpgsqlDataSource.Create(database.ConnectionString(databaseName));
        await using NpgsqlCommand command = dataSource.CreateCommand("SELECT tenant_id FROM tenant_settings");
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(CancellationToken);

        List<Guid> owners = [];
        while (await reader.ReadAsync(CancellationToken))
        {
            owners.Add(reader.GetGuid(0));
        }

        return owners;
    }

    private async Task<List<string>> GrantedAsync(string role, string databaseName)
    {
        await using var dataSource = NpgsqlDataSource.Create(database.ConnectionString(databaseName));
        await using NpgsqlCommand command = dataSource.CreateCommand(
            """
            SELECT t.table_name || ' ' || p.privilege
            FROM information_schema.tables t
            CROSS JOIN unnest(ARRAY['SELECT', 'INSERT', 'UPDATE', 'DELETE', 'TRUNCATE']) AS p(privilege)
            WHERE t.table_schema = 'public'
              AND has_table_privilege($1, quote_ident(t.table_name), p.privilege)
            """);
        command.Parameters.Add(new NpgsqlParameter { Value = role });
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(CancellationToken);

        List<string> granted = [];
        while (await reader.ReadAsync(CancellationToken))
        {
            granted.Add(reader.GetString(0));
        }

        return granted;
    }

    private sealed record Registered(TenantId Id, string Database, string RuntimeRole);
}
