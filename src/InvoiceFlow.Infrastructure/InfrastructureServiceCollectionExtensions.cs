using InvoiceFlow.Application.Abstractions.Context;
using InvoiceFlow.Application.Abstractions.Persistence;
using InvoiceFlow.Infrastructure.Persistence;
using InvoiceFlow.Infrastructure.Persistence.Catalog;
using InvoiceFlow.Infrastructure.Persistence.Tenant;
using InvoiceFlow.Infrastructure.Tenancy;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace InvoiceFlow.Infrastructure;

public static class InfrastructureServiceCollectionExtensions
{
    public const string CatalogConnectionStringName = "Catalog";

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        string catalogConnectionString = configuration.GetConnectionString(CatalogConnectionStringName)
            ?? throw new InvalidOperationException($"Connection string '{CatalogConnectionStringName}' is missing.");

        services.AddOptions<TenancyOptions>()
            .Bind(configuration.GetSection(TenancyOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddMemoryCache();

        services.AddDbContextFactory<CatalogDbContext>(options =>
            CatalogDbContext.Configure(options, catalogConnectionString));

        services.AddScoped<ITenantContext, DevelopmentTenantContext>();
        services.AddSingleton<TenantConnectionResolver>();
        services.AddSingleton<TenantDatabaseVerifier>();
        services.AddScoped<TenantConnectionInterceptor>();
        services.AddDbContext<TenantDbContext>((provider, options) =>
            TenantDbContext.Configure(options).AddInterceptors(provider.GetRequiredService<TenantConnectionInterceptor>()));

        services.AddScoped<IInvoiceRepository, InvoiceRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        return services;
    }
}
