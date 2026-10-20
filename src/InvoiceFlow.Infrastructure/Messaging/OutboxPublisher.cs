using System.Text;
using InvoiceFlow.Domain.Tenants;
using InvoiceFlow.Infrastructure.Persistence.Catalog;
using InvoiceFlow.Infrastructure.Persistence.Tenant;
using InvoiceFlow.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InvoiceFlow.Infrastructure.Messaging;

/// <summary>
/// Drains unpublished <see cref="OutboxMessage"/> rows for every active catalog tenant.
/// </summary>
internal sealed partial class OutboxPublisher(
    IServiceScopeFactory scopeFactory,
    IDbContextFactory<CatalogDbContext> catalogFactory,
    TenantConnectionResolver connectionResolver,
    IOptions<RabbitMqOptions> options,
    TimeProvider time,
    ILoggerFactory loggerFactory,
    ILogger<OutboxPublisher> logger) : BackgroundService
{
    private static readonly TimeSpan IdleDelay = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan TenantFailureBackoff = TimeSpan.FromSeconds(30);
    private const int BatchSize = 50;

    private readonly Dictionary<Guid, DateTimeOffset> _tenantBackoffUntil = [];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (string.IsNullOrWhiteSpace(options.Value.Host))
        {
            LogIdle(logger);
            await Task.Delay(Timeout.Infinite, stoppingToken);
            return;
        }

        LogStarted(logger);

        while (!stoppingToken.IsCancellationRequested)
        {
            bool publishedAny = false;
            try
            {
                publishedAny = await DrainCycleAsync(stoppingToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                LogCycleFailed(logger, exception);
            }

            await Task.Delay(publishedAny ? TimeSpan.FromMilliseconds(250) : IdleDelay, stoppingToken);
        }
    }

    private async Task<bool> DrainCycleAsync(CancellationToken cancellationToken)
    {
        await using CatalogDbContext catalog = await catalogFactory.CreateDbContextAsync(cancellationToken);
        List<TenantId> tenants = await catalog.Tenants
            .Where(t => t.Status == TenantStatus.Active)
            .OrderBy(t => t.Id)
            .Select(t => t.Id)
            .ToListAsync(cancellationToken);

        DateTimeOffset now = time.GetUtcNow();
        bool publishedAny = false;

        foreach (TenantId tenantId in tenants)
        {
            if (_tenantBackoffUntil.TryGetValue(tenantId.Value, out DateTimeOffset until) && until > now)
            {
                continue;
            }

            try
            {
                if (await DrainTenantAsync(tenantId, cancellationToken))
                {
                    publishedAny = true;
                    _tenantBackoffUntil.Remove(tenantId.Value);
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _tenantBackoffUntil[tenantId.Value] = now + TenantFailureBackoff;
                LogTenantFailed(logger, exception, tenantId.Value);
            }
        }

        return publishedAny;
    }

    private async Task<bool> DrainTenantAsync(TenantId tenantId, CancellationToken cancellationToken)
    {
        string connectionString = await connectionResolver.GetConnectionStringAsync(tenantId, cancellationToken);
        DbContextOptionsBuilder<TenantDbContext> contextOptions =
            new DbContextOptionsBuilder<TenantDbContext>().UseLoggerFactory(loggerFactory);
        TenantDbContext.Configure(contextOptions, connectionString);

        await using TenantDbContext db = new(contextOptions.Options, new FixedTenantContext(tenantId));
        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
        RabbitMqBroker broker = scope.ServiceProvider.GetRequiredService<RabbitMqBroker>();

        await using IDbContextTransaction transaction =
            await db.Database.BeginTransactionAsync(cancellationToken);

        List<OutboxMessage> batch = await db.OutboxMessages
            .FromSql(
                $"""
                 SELECT * FROM outbox_messages
                 WHERE published_at IS NULL
                 ORDER BY created_at
                 FOR UPDATE SKIP LOCKED
                 LIMIT {BatchSize}
                 """)
            .ToListAsync(cancellationToken);

        if (batch.Count == 0)
        {
            await transaction.RollbackAsync(cancellationToken);
            return false;
        }

        DateTimeOffset publishedAt = time.GetUtcNow();
        foreach (OutboxMessage message in batch)
        {
            message.Attempts++;
            byte[] body = Encoding.UTF8.GetBytes(message.Payload);
            await broker.PublishAsync(
                InvoiceFlowTopology.EventsExchange,
                InvoiceFlowTopology.UploadedRoutingKey,
                body,
                headers: MessagingTraceContext.ToRabbitHeaders(message.Headers),
                messageId: message.Id.ToString("D"),
                cancellationToken);

            message.PublishedAt = publishedAt;
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        LogDrained(logger, tenantId.Value, batch.Count);
        return true;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "RabbitMq:Host is not configured; outbox publisher is idle")]
    private static partial void LogIdle(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Outbox publisher started")]
    private static partial void LogStarted(ILogger logger);

    [LoggerMessage(Level = LogLevel.Error, Message = "Outbox publisher cycle failed")]
    private static partial void LogCycleFailed(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Outbox drain failed for tenant {TenantId}; backing off")]
    private static partial void LogTenantFailed(ILogger logger, Exception exception, Guid tenantId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Published {Count} outbox message(s) for tenant {TenantId}")]
    private static partial void LogDrained(ILogger logger, Guid tenantId, int count);
}
