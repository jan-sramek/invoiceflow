using InvoiceFlow.Application.Abstractions.Storage;
using InvoiceFlow.Domain.Tenants;
using InvoiceFlow.Infrastructure;
using InvoiceFlow.Infrastructure.Tenancy;
using InvoiceFlow.Integration.Tests.Persistence;
using InvoiceFlow.Testing.Hosting;
using InvoiceFlow.Testing.Storage;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace InvoiceFlow.Integration.Tests.Api;

/// <summary>
/// The API in memory, acting for one tenant of the shared PostgreSQL server, with documents kept in memory.
/// </summary>
public sealed class TenantApi(PostgresFixture database, TenantId tenant) : TestServiceFactory<Program>
{
    public InMemoryDocumentStorage Storage { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureTestServices(services => services.AddSingleton<IDocumentStorage>(Storage));
        builder.UseSetting(
            $"ConnectionStrings:{InfrastructureServiceCollectionExtensions.CatalogConnectionStringName}",
            database.CatalogConnectionString);
        builder.UseSetting($"{TenancyOptions.SectionName}:{nameof(TenancyOptions.CredentialsPath)}", database.CredentialsPath);
        builder.UseSetting($"{TenancyOptions.SectionName}:{nameof(TenancyOptions.DevelopmentTenantId)}", tenant.Value.ToString());
    }
}
