using InvoiceFlow.Domain.Tenants;
using InvoiceFlow.Infrastructure;
using InvoiceFlow.Infrastructure.Tenancy;
using InvoiceFlow.Integration.Tests.Persistence;
using InvoiceFlow.Testing.Hosting;
using Microsoft.AspNetCore.Hosting;

namespace InvoiceFlow.Integration.Tests.Api;

/// <summary>
/// The API in memory, acting for one tenant of the shared PostgreSQL server.
/// </summary>
public sealed class TenantApi(PostgresFixture database, TenantId tenant) : TestServiceFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting(
            $"ConnectionStrings:{InfrastructureServiceCollectionExtensions.CatalogConnectionStringName}",
            database.CatalogConnectionString);
        builder.UseSetting($"{TenancyOptions.SectionName}:{nameof(TenancyOptions.CredentialsPath)}", database.CredentialsPath);
        builder.UseSetting($"{TenancyOptions.SectionName}:{nameof(TenancyOptions.DevelopmentTenantId)}", tenant.Value.ToString());
    }
}
