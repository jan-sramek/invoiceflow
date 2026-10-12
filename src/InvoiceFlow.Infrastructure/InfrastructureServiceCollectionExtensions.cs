using InvoiceFlow.Application.Abstractions.Persistence;
using InvoiceFlow.Infrastructure.Persistence;
using InvoiceFlow.Infrastructure.Persistence.Tenant;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace InvoiceFlow.Infrastructure;

public static class InfrastructureServiceCollectionExtensions
{
    public const string TenantConnectionStringName = "Tenant";

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        string connectionString = configuration.GetConnectionString(TenantConnectionStringName)
            ?? throw new InvalidOperationException($"Connection string '{TenantConnectionStringName}' is missing.");

        services.AddDbContext<TenantDbContext>(options => TenantDbContext.Configure(options, connectionString));
        services.AddScoped<IInvoiceRepository, InvoiceRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        return services;
    }
}
