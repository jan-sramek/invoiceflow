using System.Text.Json;
using InvoiceFlow.Application.Abstractions.Context;
using InvoiceFlow.Domain.Tenants;
using InvoiceFlow.Infrastructure;
using InvoiceFlow.Infrastructure.Persistence.Catalog;
using InvoiceFlow.Infrastructure.Persistence.Tenant;
using InvoiceFlow.Infrastructure.Tenancy;
using InvoiceFlow.Integration.Tests.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Testcontainers.PostgreSql;

[assembly: AssemblyFixture(typeof(PostgresFixture))]

namespace InvoiceFlow.Integration.Tests.Persistence;

/// <summary>
/// One PostgreSQL server for the whole test run with a migrated catalog. Each test tenant gets its own
/// database, cloned from a migrated template, so tests stay independent.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    // Same image as deploy/kubernetes/postgres.
    private const string Image =
        "postgres:18.6-alpine3.24@sha256:77f585114c32fbca283dc835b0596f4e52b51b4c6662d7810b2f4084f60a1873";

    private const string CatalogDatabase = "invoiceflow_catalog";
    private const string TemplateDatabase = "tenant_template";

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder(Image).Build();
    private readonly DirectoryInfo _credentials = Directory.CreateTempSubdirectory("invoiceflow-credentials-");
    private readonly SemaphoreSlim _cloning = new(1, 1);
    private ServiceProvider? _services;

    public string Host => _container.Hostname;

    public int Port => _container.GetMappedPublicPort(PostgreSqlBuilder.PostgreSqlPort);

    public string CatalogConnectionString => ConnectionString(CatalogDatabase);

    /// <summary>
    /// Where <see cref="CreateTenantAsync"/> writes tenant credentials, one file per database.
    /// </summary>
    public string CredentialsPath => _credentials.FullName;

    /// <summary>
    /// Root DI container used by <see cref="OpenScope"/> (catalog factory, connection resolver, etc.).
    /// </summary>
    internal IServiceProvider Services =>
        _services ?? throw new InvalidOperationException("Not initialised.");

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();
        await ExecuteAsync("postgres", $"CREATE DATABASE {CatalogDatabase}", $"CREATE DATABASE {TemplateDatabase}");

        await using (var catalog = new CatalogDbContext(Options<CatalogDbContext>(o =>
            CatalogDbContext.Configure(o, ConnectionString(CatalogDatabase)))))
        {
            await catalog.Database.MigrateAsync();
        }

        // Pooling off, because PostgreSQL only copies a template database that has no open connections.
        await using (var template = new TenantDbContext(
            Options<TenantDbContext>(o => TenantDbContext.Configure(o, ConnectionString(TemplateDatabase, pooling: false))),
            new FixedTenant(default)))
        {
            await template.Database.MigrateAsync();
        }

        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"ConnectionStrings:{InfrastructureServiceCollectionExtensions.CatalogConnectionStringName}"] =
                    ConnectionString(CatalogDatabase),
                [$"{TenancyOptions.SectionName}:{nameof(TenancyOptions.CredentialsPath)}"] = _credentials.FullName,
            })
            .Build();

        _services = new ServiceCollection()
            .AddInfrastructure(configuration)
            .AddScoped<FixedTenant>(_ => new FixedTenant(default))
            .AddScoped<ITenantContext>(provider => provider.GetRequiredService<FixedTenant>())
            .BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
    }

    /// <summary>
    /// Registers an active tenant with its own migrated database and credentials.
    /// </summary>
    public async Task<TestTenant> CreateTenantAsync()
    {
        var tenantId = new TenantId(Guid.CreateVersion7());
        string database = $"tenant_{tenantId.Value:N}";

        await _cloning.WaitAsync();
        try
        {
            await ExecuteAsync("postgres", $"CREATE DATABASE {database} TEMPLATE {TemplateDatabase}");
        }
        finally
        {
            _cloning.Release();
        }

        await ExecuteAsync(database, $"INSERT INTO tenant_settings (tenant_id) VALUES ('{tenantId.Value}')");
        await WriteCredentialsAsync(_credentials.FullName, database);
        await UpdateCatalogAsync(catalog =>
            catalog.Tenants.AddAsync(CatalogEntry(tenantId, database, runtimeRole: Server.Username!)).AsTask());

        return new TestTenant(tenantId, database);
    }

    /// <summary>
    /// An active tenant whose credentials key is the database name.
    /// </summary>
    internal CatalogTenant CatalogEntry(TenantId tenantId, string database, string runtimeRole) => new()
    {
        Id = tenantId,
        Name = $"Tenant {tenantId}",
        Slug = $"tenant-{tenantId.Value:N}",
        Status = TenantStatus.Active,
        CreatedAt = DateTimeOffset.UnixEpoch,
        Database = new TenantDatabase
        {
            TenantId = tenantId,
            Location = DatabaseLocation.Managed,
            Host = Host,
            Port = Port,
            DatabaseName = database,
            SslMode = SslMode.Disable,
            CredentialsKey = database,
            RuntimeRole = runtimeRole,
        },
    };

    /// <summary>
    /// Writes the server's superuser login as the credentials file <paramref name="key"/>.
    /// </summary>
    internal Task WriteCredentialsAsync(string directory, string key) =>
        File.WriteAllTextAsync(
            Path.Combine(directory, key),
            JsonSerializer.Serialize(new { username = Server.Username, password = Server.Password }));

    internal async Task UpdateCatalogAsync(Func<CatalogDbContext, Task> change)
    {
        await using var catalog = new CatalogDbContext(Options<CatalogDbContext>(o =>
            CatalogDbContext.Configure(o, ConnectionString(CatalogDatabase))));
        await change(catalog);
        await catalog.SaveChangesAsync();
    }

    /// <summary>
    /// A context on <paramref name="database"/> acting for <paramref name="tenantId"/>, without routing
    /// or the ownership check, to test the query filter on its own.
    /// </summary>
    internal TenantDbContext OpenUnrouted(string database, TenantId tenantId) =>
        new(Options<TenantDbContext>(o => TenantDbContext.Configure(o, ConnectionString(database))), new FixedTenant(tenantId));

    /// <summary>
    /// A scope acting for <paramref name="tenantId"/>, like one request or message.
    /// </summary>
    public AsyncServiceScope OpenScope(TenantId tenantId)
    {
        AsyncServiceScope scope = (_services ?? throw new InvalidOperationException("Not initialised.")).CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<FixedTenant>().TenantId = tenantId;
        return scope;
    }

    public string ConnectionString(string database, bool pooling = true) =>
        new NpgsqlConnectionStringBuilder(_container.GetConnectionString())
        {
            Database = database,
            Pooling = pooling,
        }.ConnectionString;

    private NpgsqlConnectionStringBuilder Server => new(_container.GetConnectionString());

    public async ValueTask DisposeAsync()
    {
        if (_services is not null)
        {
            await _services.DisposeAsync();
        }

        await _container.DisposeAsync();
        _credentials.Delete(recursive: true);
        _cloning.Dispose();
    }

    private static DbContextOptions<TContext> Options<TContext>(Action<DbContextOptionsBuilder> configure)
        where TContext : DbContext
    {
        var builder = new DbContextOptionsBuilder<TContext>();
        configure(builder);
        return builder.Options;
    }

    internal async Task ExecuteAsync(string database, params string[] statements)
    {
        await using var connection = new NpgsqlConnection(ConnectionString(database, pooling: false));
        await connection.OpenAsync();

        foreach (string statement in statements)
        {
            await using var command = new NpgsqlCommand(statement, connection);
            await command.ExecuteNonQueryAsync();
        }
    }

    private sealed class FixedTenant(TenantId tenantId) : ITenantContext
    {
        public TenantId TenantId { get; set; } = tenantId;
    }
}

public sealed record TestTenant(TenantId Id, string Database);
