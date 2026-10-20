using System.Security.Cryptography;
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
/// On shutdown, stops consuming first, then waits for in-flight work (unacked messages requeue on channel close).
/// </summary>
internal sealed partial class InvoiceProcessingConsumer(
    IServiceScopeFactory scopeFactory,
    IServiceProvider services,
    IOptions<RabbitMqOptions> options,
    ILogger<InvoiceProcessingConsumer> logger) : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Time allowed for in-flight extraction after consume is cancelled (under the host shutdown budget).
    /// </summary>
    internal static readonly TimeSpan InFlightDrainTimeout = TimeSpan.FromMinutes(2);

    private Task? _inFlight;
    private readonly object _inFlightGate = new();

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

        string consumerTag = await channel.BasicConsumeAsync(
            queue: InvoiceFlowTopology.ProcessingQueue,
            autoAck: false,
            consumer: consumer,
            cancellationToken: stoppingToken);

        LogConsuming(logger, InvoiceFlowTopology.ProcessingQueue);

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            LogShuttingDown(logger);
        }

        // Stop new deliveries first; any still-unacked message is requeued when the channel closes.
        try
        {
            await channel.BasicCancelAsync(consumerTag, cancellationToken: CancellationToken.None);
            LogConsumeCancelled(logger, consumerTag);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogCancelFailed(logger, exception);
        }

        await DrainInFlightAsync();

        async Task OnReceivedAsync(object sender, BasicDeliverEventArgs args)
        {
            Task processing = HandleDeliveryAsync(channel, broker, args, stoppingToken);
            lock (_inFlightGate)
            {
                _inFlight = processing;
            }

            try
            {
                await processing;
            }
            finally
            {
                lock (_inFlightGate)
                {
                    if (ReferenceEquals(_inFlight, processing))
                    {
                        _inFlight = null;
                    }
                }
            }
        }
    }

    private async Task DrainInFlightAsync()
    {
        Task? pending;
        lock (_inFlightGate)
        {
            pending = _inFlight;
        }

        if (pending is null)
        {
            return;
        }

        try
        {
            await pending.WaitAsync(InFlightDrainTimeout, CancellationToken.None);
            LogInFlightFinished(logger);
        }
        catch (TimeoutException)
        {
            LogInFlightTimedOut(logger, InFlightDrainTimeout);
        }
        catch (OperationCanceledException)
        {
            LogInFlightCancelled(logger);
        }
    }

    private async Task HandleDeliveryAsync(
        IChannel channel,
        RabbitMqBroker broker,
        BasicDeliverEventArgs args,
        CancellationToken stoppingToken)
    {
        int attempt = ReadAttempt(args);
        byte[] body = args.Body.ToArray();
        Guid messageId = ResolveMessageId(args, body);

        InvoiceUploadedMessage? message;
        try
        {
            message = JsonSerializer.Deserialize<InvoiceUploadedMessage>(body, JsonOptions);
        }
        catch (JsonException exception)
        {
            LogInvalidPayload(logger, exception);
            await PublishDeadLetterAsync(
                broker, body, args.BasicProperties.Headers, attempt, messageId, CancellationToken.None);
            await channel.BasicAckAsync(args.DeliveryTag, multiple: false, cancellationToken: CancellationToken.None);
            return;
        }

        if (message is null || message.TenantId == Guid.Empty || message.InvoiceId == Guid.Empty)
        {
            LogMissingIds(logger);
            await PublishDeadLetterAsync(
                broker, body, args.BasicProperties.Headers, attempt, messageId, CancellationToken.None);
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
                await processor.ProcessAsync(messageId, invoiceId, stoppingToken);
            }

            await channel.BasicAckAsync(args.DeliveryTag, multiple: false, cancellationToken: CancellationToken.None);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Leave unacked so the broker requeues when the channel closes.
            LogShutdownInterrupted(logger, invoiceId.Value);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogProcessingFailed(logger, exception, invoiceId.Value, attempt);

            string? retryKey = RetryRouting.NextRetryRoutingKey(attempt);
            if (retryKey is not null)
            {
                await PublishRetryAsync(broker, body, attempt + 1, retryKey, messageId, CancellationToken.None);
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
                        messageId, invoiceId, "Extraction failed after repeated attempts", CancellationToken.None);
                }
            }
            catch (Exception failException)
            {
                LogFailMarkFailed(logger, failException, invoiceId.Value);
            }

            await PublishDeadLetterAsync(
                broker, body, args.BasicProperties.Headers, attempt, messageId, CancellationToken.None);
            await channel.BasicAckAsync(args.DeliveryTag, multiple: false, cancellationToken: CancellationToken.None);
        }
    }

    private async Task PublishRetryAsync(
        RabbitMqBroker broker,
        byte[] body,
        int nextAttempt,
        string routingKey,
        Guid messageId,
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
            messageId: messageId.ToString("D"),
            cancellationToken);

        LogRetryScheduled(logger, routingKey, nextAttempt);
    }

    private async Task PublishDeadLetterAsync(
        RabbitMqBroker broker,
        byte[] body,
        IDictionary<string, object?>? existingHeaders,
        int attempt,
        Guid messageId,
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
            messageId: messageId.ToString("D"),
            cancellationToken);

        LogDeadLetter(logger, attempt);
    }

    /// <summary>
    /// Prefer the outbox id from <c>message_id</c>; otherwise a stable hash of the body for legacy publishes.
    /// </summary>
    private static Guid ResolveMessageId(BasicDeliverEventArgs args, byte[] body)
    {
        if (!string.IsNullOrWhiteSpace(args.BasicProperties.MessageId)
            && Guid.TryParse(args.BasicProperties.MessageId, out Guid parsed))
        {
            return parsed;
        }

        return new Guid(SHA256.HashData(body).AsSpan(0, 16));
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

    [LoggerMessage(Level = LogLevel.Information, Message = "Shutdown requested; cancelling consumer and draining in-flight work")]
    private static partial void LogShuttingDown(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Cancelled consumer {ConsumerTag}")]
    private static partial void LogConsumeCancelled(ILogger logger, string consumerTag);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to cancel RabbitMQ consumer during shutdown")]
    private static partial void LogCancelFailed(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Information, Message = "In-flight invoice processing finished during shutdown")]
    private static partial void LogInFlightFinished(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "In-flight invoice processing did not finish within {Timeout}; message will be requeued")]
    private static partial void LogInFlightTimedOut(ILogger logger, TimeSpan timeout);

    [LoggerMessage(Level = LogLevel.Information, Message = "In-flight invoice processing cancelled during shutdown; message will be requeued")]
    private static partial void LogInFlightCancelled(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Shutdown interrupted processing of invoice {InvoiceId}; leaving message unacked for requeue")]
    private static partial void LogShutdownInterrupted(ILogger logger, Guid invoiceId);

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
