using InvoiceFlow.Application.Abstractions.Context;
using InvoiceFlow.Domain.Tenants;
using InvoiceFlow.Infrastructure;
using InvoiceFlow.Infrastructure.Persistence.Tenant;
using InvoiceFlow.Integration.Tests.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

[assembly: AssemblyFixture(typeof(PostgresFixture))]

namespace InvoiceFlow.Integration.Tests.Persistence;

/// <summary>
/// One migrated PostgreSQL for the whole test run. Tests stay independent by using a new tenant id each.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    // Same image as deploy/kubernetes/postgres.
    private const string Image =
        "postgres:18.6-alpine3.24@sha256:77f585114c32fbca283dc835b0596f4e52b51b4c6662d7810b2f4084f60a1873";

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder(Image).Build();
    private ServiceProvider? _services;

    public string ConnectionString => _container.GetConnectionString();

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();

        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"ConnectionStrings:{InfrastructureServiceCollectionExtensions.TenantConnectionStringName}"] =
                    ConnectionString,
            })
            .Build();

        _services = new ServiceCollection()
            .AddInfrastructure(configuration)
            .AddScoped<TestTenantContext>()
            .AddScoped<ITenantContext>(provider => provider.GetRequiredService<TestTenantContext>())
            .BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });

        await using AsyncServiceScope scope = OpenScope(new TenantId(Guid.NewGuid()));
        await scope.ServiceProvider.GetRequiredService<TenantDbContext>().Database.MigrateAsync();
    }

    /// <summary>
    /// A scope acting for <paramref name="tenantId"/>, like one request or message.
    /// </summary>
    public AsyncServiceScope OpenScope(TenantId tenantId)
    {
        AsyncServiceScope scope = (_services ?? throw new InvalidOperationException("Not initialised.")).CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TestTenantContext>().TenantId = tenantId;
        return scope;
    }

    public async ValueTask DisposeAsync()
    {
        if (_services is not null)
        {
            await _services.DisposeAsync();
        }

        await _container.DisposeAsync();
    }

    private sealed class TestTenantContext : ITenantContext
    {
        public TenantId TenantId { get; set; }
    }
}
