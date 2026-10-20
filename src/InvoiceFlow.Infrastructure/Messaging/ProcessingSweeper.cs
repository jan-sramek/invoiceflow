using System.Text.Json;
using InvoiceFlow.Application.Audit;
using InvoiceFlow.Domain.Invoices;
using InvoiceFlow.Domain.Tenants;
using InvoiceFlow.Infrastructure.Persistence.Catalog;
using InvoiceFlow.Infrastructure.Persistence.Tenant;
using InvoiceFlow.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InvoiceFlow.Infrastructure.Messaging;

/// <summary>
/// Re-queues invoices stuck in <see cref="InvoiceStatus.Processing"/> by writing a fresh outbox row.
/// </summary>
internal sealed partial class ProcessingSweeper(
    IDbContextFactory<CatalogDbContext> catalogFactory,
    TenantConnectionResolver connectionResolver,
    IOptions<ProcessingSweeperOptions> options,
    IOptions<RabbitMqOptions> rabbitMq,
    TimeProvider time,
    ILoggerFactory loggerFactory,
    ILogger<ProcessingSweeper> logger) : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly TimeSpan TenantFailureBackoff = TimeSpan.FromSeconds(30);

    private readonly Dictionary<Guid, DateTimeOffset> _tenantBackoffUntil = [];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (string.IsNullOrWhiteSpace(rabbitMq.Value.Host))
        {
            LogIdle(logger);
            await Task.Delay(Timeout.Infinite, stoppingToken);
            return;
        }

        LogStarted(logger, options.Value.StuckAfter, options.Value.PollInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SweepCycleAsync(stoppingToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                LogCycleFailed(logger, exception);
            }

            await Task.Delay(options.Value.PollInterval, stoppingToken);
        }
    }

    /// <summary>
    /// One pass over active tenants. Exposed for tests.
    /// </summary>
    internal async Task<int> SweepCycleAsync(CancellationToken cancellationToken)
    {
        await using CatalogDbContext catalog = await catalogFactory.CreateDbContextAsync(cancellationToken);
        List<TenantId> tenants = await catalog.Tenants
            .Where(t => t.Status == TenantStatus.Active)
            .OrderBy(t => t.Id)
            .Select(t => t.Id)
            .ToListAsync(cancellationToken);

        DateTimeOffset now = time.GetUtcNow();
        int requeued = 0;

        foreach (TenantId tenantId in tenants)
        {
            if (_tenantBackoffUntil.TryGetValue(tenantId.Value, out DateTimeOffset until) && until > now)
            {
                continue;
            }

            try
            {
                int count = await SweepTenantAsync(tenantId, cancellationToken);
                if (count > 0)
                {
                    requeued += count;
                    _tenantBackoffUntil.Remove(tenantId.Value);
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _tenantBackoffUntil[tenantId.Value] = now + TenantFailureBackoff;
                LogTenantFailed(logger, exception, tenantId.Value);
            }
        }

        return requeued;
    }

    internal async Task<int> SweepTenantAsync(TenantId tenantId, CancellationToken cancellationToken)
    {
        ProcessingSweeperOptions settings = options.Value;
        DateTimeOffset cutoff = time.GetUtcNow() - settings.StuckAfter;

        string connectionString = await connectionResolver.GetConnectionStringAsync(tenantId, cancellationToken);
        DbContextOptionsBuilder<TenantDbContext> contextOptions =
            new DbContextOptionsBuilder<TenantDbContext>().UseLoggerFactory(loggerFactory);
        TenantDbContext.Configure(contextOptions, connectionString);

        await using TenantDbContext db = new(contextOptions.Options, new FixedTenantContext(tenantId));

        List<Guid> stuckIds = await FindStuckInvoiceIdsAsync(db, cutoff, settings.BatchSize, cancellationToken);
        if (stuckIds.Count == 0)
        {
            return 0;
        }

        DateTimeOffset now = time.GetUtcNow();
        foreach (Guid invoiceId in stuckIds)
        {
            string payload = JsonSerializer.Serialize(
                new InvoiceUploadedMessage(tenantId.Value, invoiceId), JsonOptions);
            db.OutboxMessages.Add(new OutboxMessage
            {
                Id = Guid.CreateVersion7(now),
                Type = OutboxMessage.InvoiceUploadedType,
                Payload = payload,
                CreatedAt = now,
            });
        }

        await db.SaveChangesAsync(cancellationToken);
        LogRequeued(logger, tenantId.Value, stuckIds.Count);
        return stuckIds.Count;
    }

    private static async Task<List<Guid>> FindStuckInvoiceIdsAsync(
        TenantDbContext db,
        DateTimeOffset cutoff,
        int batchSize,
        CancellationToken cancellationToken)
    {
        string processingStarted = AuditActions.InvoiceProcessingStarted;
        string extractionRetried = AuditActions.InvoiceExtractionRetried;
        string outboxType = OutboxMessage.InvoiceUploadedType;

        // Prefer invoices whose last Processing entry is older than the cutoff, and that do not
        // already have an unpublished outbox row (avoids duplicate re-queues while drain lags).
        return await db.Database
            .SqlQuery<Guid>(
                $"""
                 SELECT i.id AS "Value"
                 FROM invoices i
                 INNER JOIN LATERAL (
                     SELECT a.occurred_at
                     FROM audit_events a
                     WHERE a.subject_id = i.id
                       AND a.action IN ({processingStarted}, {extractionRetried})
                     ORDER BY a.occurred_at DESC
                     LIMIT 1
                 ) started ON true
                 WHERE i.status = {"Processing"}
                   AND started.occurred_at < {cutoff}
                   AND NOT EXISTS (
                       SELECT 1
                       FROM outbox_messages o
                       WHERE o.published_at IS NULL
                         AND o.type = {outboxType}
                         AND o.payload->>'invoiceId' = i.id::text)
                 ORDER BY started.occurred_at
                 LIMIT {batchSize}
                 """)
            .ToListAsync(cancellationToken);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "RabbitMq:Host is not configured; processing sweeper is idle")]
    private static partial void LogIdle(ILogger logger);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Processing sweeper started (stuckAfter={StuckAfter}, pollInterval={PollInterval})")]
    private static partial void LogStarted(ILogger logger, TimeSpan stuckAfter, TimeSpan pollInterval);

    [LoggerMessage(Level = LogLevel.Error, Message = "Processing sweeper cycle failed")]
    private static partial void LogCycleFailed(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Processing sweep failed for tenant {TenantId}; backing off")]
    private static partial void LogTenantFailed(ILogger logger, Exception exception, Guid tenantId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Re-queued {Count} stuck Processing invoice(s) for tenant {TenantId}")]
    private static partial void LogRequeued(ILogger logger, Guid tenantId, int count);
}
