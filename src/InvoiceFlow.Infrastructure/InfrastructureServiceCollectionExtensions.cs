using InvoiceFlow.Application.Abstractions.Context;
using InvoiceFlow.Application.Abstractions.Persistence;
using InvoiceFlow.Infrastructure.Persistence;
using InvoiceFlow.Infrastructure.Persistence.Catalog;
using InvoiceFlow.Infrastructure.Persistence.SchemaMigration;
using InvoiceFlow.Infrastructure.Persistence.Tenant;
using InvoiceFlow.Infrastructure.Tenancy;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace InvoiceFlow.Infrastructure;

public static class InfrastructureServiceCollectionExtensions
{
    public const string CatalogConnectionStringName = "Catalog";

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddCatalog(configuration);
        services.AddOptions<TenancyOptions>()
            .Bind(configuration.GetSection(TenancyOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddMemoryCache();

        services.AddScoped<ITenantContext, DevelopmentTenantContext>();
        services.AddSingleton<TenantConnectionResolver>();
        services.AddSingleton<TenantDatabaseVerifier>();
        services.AddScoped<TenantConnectionInterceptor>();
        services.AddDbContext<TenantDbContext>((provider, options) =>
            TenantDbContext.Configure(options).AddInterceptors(provider.GetRequiredService<TenantConnectionInterceptor>()));

        services.AddScoped<IInvoiceRepository, InvoiceRepository>();
        services.AddScoped<IInvoiceReadService, InvoiceReadService>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        return services;
    }

    /// <summary>
    /// The catalog connection string here must belong to the role that owns the catalog schema.
    /// </summary>
    public static IServiceCollection AddSchemaMigrator(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddCatalog(configuration);
        services.AddOptions<SchemaMigrationOptions>()
            .Bind(configuration.GetSection(SchemaMigrationOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<ISchemaMigrator, SchemaMigrator>();

        return services;
    }

    public static IHealthChecksBuilder AddCatalogCheck(this IHealthChecksBuilder builder, params string[] tags) =>
        builder.AddCheck<CatalogHealthCheck>("catalog", tags: tags);

    private static void AddCatalog(this IServiceCollection services, IConfiguration configuration)
    {
        string connectionString = configuration.GetConnectionString(CatalogConnectionStringName)
            ?? throw new InvalidOperationException($"Connection string '{CatalogConnectionStringName}' is missing.");

        services.AddDbContextFactory<CatalogDbContext>(options => CatalogDbContext.Configure(options, connectionString));
    }
}
