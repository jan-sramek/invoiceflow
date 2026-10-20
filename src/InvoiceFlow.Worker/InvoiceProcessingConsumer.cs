using System.Text.Json;
using InvoiceFlow.Domain.Invoices;
using InvoiceFlow.Domain.Tenants;
using InvoiceFlow.Infrastructure.Messaging;
using InvoiceFlow.Infrastructure.Tenancy;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace InvoiceFlow.Worker;

/// <summary>
/// Consumes <c>invoice.uploaded</c> messages and runs <see cref="InvoiceProcessingService"/>.
/// Technical failures publish to TTL retry queues (or DLQ when exhausted); business outcomes ack.
/// </summary>
internal sealed partial class InvoiceProcessingConsumer(
    IServiceScopeFactory scopeFactory,
    IServiceProvider services,
    IOptions<RabbitMqOptions> options,
    ILogger<InvoiceProcessingConsumer> logger) : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        RabbitMqOptions settings = options.Value;
        RabbitMqBroker? broker = services.GetService<RabbitMqBroker>();
        if (string.IsNullOrWhiteSpace(settings.Host) || broker is null)
        {
            LogIdle(logger);
            await Task.Delay(Timeout.Infinite, stoppingToken);
            return;
        }

        var factory = new ConnectionFactory
        {
            HostName = settings.Host,
            Port = settings.Port,
            VirtualHost = settings.VirtualHost,
            UserName = settings.UserName,
            Password = settings.Password,
        };

        await using IConnection connection = await factory.CreateConnectionAsync(stoppingToken);
        await using IChannel channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);
        await channel.BasicQosAsync(prefetchSize: 0, prefetchCount: 1, global: false, cancellationToken: stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += OnReceivedAsync;

        await channel.BasicConsumeAsync(
            queue: InvoiceFlowTopology.ProcessingQueue,
            autoAck: false,
            consumer: consumer,
            cancellationToken: stoppingToken);

        LogConsuming(logger, InvoiceFlowTopology.ProcessingQueue);
        await Task.Delay(Timeout.Infinite, stoppingToken);

        async Task OnReceivedAsync(object sender, BasicDeliverEventArgs args)
        {
            int attempt = ReadAttempt(args);
            byte[] body = args.Body.ToArray();

            InvoiceUploadedMessage? message;
            try
            {
                message = JsonSerializer.Deserialize<InvoiceUploadedMessage>(body, JsonOptions);
            }
            catch (JsonException exception)
            {
                LogInvalidPayload(logger, exception);
                await PublishDeadLetterAsync(broker, body, args.BasicProperties.Headers, attempt, stoppingToken);
                await channel.BasicAckAsync(args.DeliveryTag, multiple: false, cancellationToken: CancellationToken.None);
                return;
            }

            if (message is null || message.TenantId == Guid.Empty || message.InvoiceId == Guid.Empty)
            {
                LogMissingIds(logger);
                await PublishDeadLetterAsync(broker, body, args.BasicProperties.Headers, attempt, stoppingToken);
                await channel.BasicAckAsync(args.DeliveryTag, multiple: false, cancellationToken: CancellationToken.None);
                return;
            }

            var tenantId = new TenantId(message.TenantId);
            var invoiceId = new InvoiceId(message.InvoiceId);

            try
            {
                using (MessageTenantContext.Use(tenantId))
                await using (AsyncServiceScope scope = scopeFactory.CreateAsyncScope())
                {
                    InvoiceProcessingService processor =
                        scope.ServiceProvider.GetRequiredService<InvoiceProcessingService>();
                    await processor.ProcessAsync(invoiceId, stoppingToken);
                }

                await channel.BasicAckAsync(args.DeliveryTag, multiple: false, cancellationToken: CancellationToken.None);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                LogProcessingFailed(logger, exception, invoiceId.Value, attempt);

                string? retryKey = RetryRouting.NextRetryRoutingKey(attempt);
                if (retryKey is not null)
                {
                    await PublishRetryAsync(broker, body, attempt + 1, retryKey, stoppingToken);
                    await channel.BasicAckAsync(args.DeliveryTag, multiple: false, cancellationToken: CancellationToken.None);
                    return;
                }

                try
                {
                    using (MessageTenantContext.Use(tenantId))
                    await using (AsyncServiceScope scope = scopeFactory.CreateAsyncScope())
                    {
                        InvoiceProcessingService processor =
                            scope.ServiceProvider.GetRequiredService<InvoiceProcessingService>();
                        await processor.FailAfterRetriesAsync(
                            invoiceId, "Extraction failed after repeated attempts", CancellationToken.None);
                    }
                }
                catch (Exception failException)
                {
                    LogFailMarkFailed(logger, failException, invoiceId.Value);
                }

                await PublishDeadLetterAsync(broker, body, args.BasicProperties.Headers, attempt, CancellationToken.None);
                await channel.BasicAckAsync(args.DeliveryTag, multiple: false, cancellationToken: CancellationToken.None);
            }
        }
    }

    private async Task PublishRetryAsync(
        RabbitMqBroker broker,
        byte[] body,
        int nextAttempt,
        string routingKey,
        CancellationToken cancellationToken)
    {
        Dictionary<string, object?> headers = new()
        {
            [InvoiceFlowTopology.AttemptHeader] = nextAttempt,
        };

        await broker.PublishAsync(
            InvoiceFlowTopology.RetryExchange,
            routingKey,
            body,
            headers,
            messageId: null,
            cancellationToken);

        LogRetryScheduled(logger, routingKey, nextAttempt);
    }

    private async Task PublishDeadLetterAsync(
        RabbitMqBroker broker,
        byte[] body,
        IDictionary<string, object?>? existingHeaders,
        int attempt,
        CancellationToken cancellationToken)
    {
        Dictionary<string, object?> headers = existingHeaders is null
            ? new Dictionary<string, object?>()
            : new Dictionary<string, object?>(existingHeaders);
        headers[InvoiceFlowTopology.AttemptHeader] = attempt;

        await broker.PublishAsync(
            InvoiceFlowTopology.DlxExchange,
            InvoiceFlowTopology.DeadLetterRoutingKey,
            body,
            headers,
            messageId: null,
            cancellationToken);

        LogDeadLetter(logger, attempt);
    }

    private static int ReadAttempt(BasicDeliverEventArgs args)
    {
        if (args.BasicProperties.Headers is { } headers
            && headers.TryGetValue(InvoiceFlowTopology.AttemptHeader, out object? raw))
        {
            return raw switch
            {
                byte b => b,
                sbyte sb => sb,
                short s => s,
                int i => i,
                long l => (int)l,
                byte[] bytes when bytes.Length > 0 => bytes[0],
                _ => 0,
            };
        }

        return 0;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "RabbitMq:Host is not configured; invoice processing consumer is idle")]
    private static partial void LogIdle(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Consuming queue {Queue}")]
    private static partial void LogConsuming(ILogger logger, string queue);

    [LoggerMessage(Level = LogLevel.Error, Message = "Invalid invoice.uploaded payload; routing to DLQ")]
    private static partial void LogInvalidPayload(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "invoice.uploaded payload missing ids; routing to DLQ")]
    private static partial void LogMissingIds(ILogger logger);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed processing invoice {InvoiceId} (attempt {Attempt})")]
    private static partial void LogProcessingFailed(ILogger logger, Exception exception, Guid invoiceId, int attempt);

    [LoggerMessage(Level = LogLevel.Error, Message = "Could not mark invoice {InvoiceId} as extraction failed")]
    private static partial void LogFailMarkFailed(ILogger logger, Exception exception, Guid invoiceId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Scheduled retry via {RoutingKey} (next attempt {Attempt})")]
    private static partial void LogRetryScheduled(ILogger logger, string routingKey, int attempt);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Published message to DLQ after attempt {Attempt}")]
    private static partial void LogDeadLetter(ILogger logger, int attempt);
}
