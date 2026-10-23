using InvoiceFlow.Application.Abstractions.Context;
using InvoiceFlow.Application.Abstractions.Documents;
using InvoiceFlow.Application.Abstractions.Extraction;
using InvoiceFlow.Application.Abstractions.Messaging;
using InvoiceFlow.Application.Abstractions.Persistence;
using InvoiceFlow.Infrastructure.Documents;
using InvoiceFlow.Infrastructure.Extraction;
using InvoiceFlow.Infrastructure.Messaging;
using InvoiceFlow.Infrastructure.Persistence;
using InvoiceFlow.Infrastructure.Persistence.Catalog;
using InvoiceFlow.Infrastructure.Persistence.Idempotency;
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
        services.AddMetrics();
        services.TryAddSingleton(TimeProvider.System);

        services.AddScoped<ITenantContext, DevelopmentTenantContext>();
        services.AddSingleton<TenantConnectionResolver>();
        services.AddSingleton<TenantDatabaseVerifier>();
        services.AddSingleton<TenantDatabaseResilience>();
        services.AddSingleton<TenantDatabaseMetrics>();
        services.AddScoped<TenantConnectionInterceptor>();
        services.AddDbContext<TenantDbContext>((provider, options) =>
            TenantDbContext.Configure(options).AddInterceptors(provider.GetRequiredService<TenantConnectionInterceptor>()));

        services.AddScoped<IInvoiceRepository, InvoiceRepository>();
        services.AddScoped<IInvoiceReadService, InvoiceReadService>();
        services.AddScoped<IIdempotencyStore, IdempotencyStore>();
        services.AddScoped<IProcessedMessageStore, ProcessedMessageStore>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddDocumentStorage(configuration);
        services.AddMessaging(configuration);
        services.AddExtraction(configuration);

        return services;
    }

    /// <summary>
    /// Uses the tenant id from each RabbitMQ message instead of a fixed development tenant.
    /// </summary>
    public static IServiceCollection AddMessageTenantContext(this IServiceCollection services)
    {
        services.RemoveAll<ITenantContext>();
        services.AddScoped<ITenantContext, MessageTenantContext>();
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

    /// <summary>
    /// Replaces S3 with an in-memory store. Call after <see cref="AddInfrastructure"/>.
    /// </summary>
    public static IServiceCollection AddInMemoryDocumentStorage(this IServiceCollection services)
    {
        services.RemoveAll<IDocumentStorage>();
        services.AddSingleton<IDocumentStorage, InMemoryDocumentStorage>();
        return services;
    }

    private static void AddDocumentStorage(this IServiceCollection services, IConfiguration configuration)
    {
        IConfigurationSection section = configuration.GetSection(DocumentStorageOptions.SectionName);
        if (!section.Exists())
        {
            services.AddSingleton<IDocumentStorage, InMemoryDocumentStorage>();
            return;
        }

        // RootPath: shared disk for local API + Worker (InMemory is process-local and breaks extraction).
        if (!string.IsNullOrWhiteSpace(section["RootPath"]))
        {
            services.AddOptions<FileSystemDocumentStorageOptions>()
                .Bind(section)
                .ValidateDataAnnotations()
                .ValidateOnStart();
            services.AddSingleton<IDocumentStorage, FileSystemDocumentStorage>();
            return;
        }

        if (string.IsNullOrWhiteSpace(section["ServiceUrl"]))
        {
            services.AddSingleton<IDocumentStorage, InMemoryDocumentStorage>();
            return;
        }

        services.AddOptions<DocumentStorageOptions>()
            .Bind(section)
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddSingleton<IDocumentStorage, S3DocumentStorage>();
    }

    /// <summary>
    /// Hosts the outbox drain loop. Call from the API only when RabbitMQ is configured.
    /// </summary>
    public static IServiceCollection AddOutboxPublisher(this IServiceCollection services)
    {
        services.AddHostedService<OutboxPublisher>();
        return services;
    }

    /// <summary>
    /// Hosts the stuck-Processing sweeper. Call from the worker when RabbitMQ is configured.
    /// </summary>
    public static IServiceCollection AddProcessingSweeper(this IServiceCollection services, IConfiguration? configuration = null)
    {
        if (configuration is not null)
        {
            services.AddOptions<ProcessingSweeperOptions>()
                .Bind(configuration.GetSection(ProcessingSweeperOptions.SectionName))
                .ValidateDataAnnotations()
                .ValidateOnStart();
        }
        else
        {
            services.AddOptions<ProcessingSweeperOptions>();
        }

        services.AddHostedService<ProcessingSweeper>();
        return services;
    }

    private static void AddMessaging(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IInvoiceEventPublisher, OutboxInvoiceEventPublisher>();

        IConfigurationSection section = configuration.GetSection(RabbitMqOptions.SectionName);
        if (!section.Exists() || string.IsNullOrWhiteSpace(section["Host"]))
        {
            services.AddOptions<RabbitMqOptions>().Bind(section);
            return;
        }

        services.AddOptions<RabbitMqOptions>()
            .Bind(section)
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddSingleton<RabbitMqBroker>();
    }

    private static void AddExtraction(this IServiceCollection services, IConfiguration configuration)
    {
        IConfigurationSection section = configuration.GetSection(DocumentExtractionOptions.SectionName);
        if (!section.Exists() || string.IsNullOrWhiteSpace(section["ServiceUrl"]))
        {
            services.AddOptions<FakeExtractorOptions>()
                .Bind(configuration.GetSection(FakeExtractorOptions.SectionName))
                .ValidateDataAnnotations()
                .ValidateOnStart();
            services.AddSingleton<IInvoiceExtractor, FakeInvoiceExtractor>();
            return;
        }

        services.AddOptions<DocumentExtractionOptions>()
            .Bind(section)
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddSingleton<PdfPigTextExtractor>();
        services.AddHttpClient<IInvoiceExtractor, OllamaInvoiceExtractor>((provider, client) =>
        {
            DocumentExtractionOptions options = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<DocumentExtractionOptions>>().Value;
            client.BaseAddress = new Uri(options.ServiceUrl.TrimEnd('/') + "/");
            client.Timeout = TimeSpan.FromMinutes(2);
        });
    }

    private static void AddCatalog(this IServiceCollection services, IConfiguration configuration)
    {
        string connectionString = configuration.GetConnectionString(CatalogConnectionStringName)
            ?? throw new InvalidOperationException($"Connection string '{CatalogConnectionStringName}' is missing.");

        services.AddDbContextFactory<CatalogDbContext>(options => CatalogDbContext.Configure(options, connectionString));
    }
}
