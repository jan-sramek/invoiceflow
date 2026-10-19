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
/// </summary>
internal sealed partial class InvoiceProcessingConsumer(
    IServiceScopeFactory scopeFactory,
    IOptions<RabbitMqOptions> options,
    ILogger<InvoiceProcessingConsumer> logger) : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        RabbitMqOptions settings = options.Value;
        if (string.IsNullOrWhiteSpace(settings.Host))
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
            InvoiceUploadedMessage? message;
            try
            {
                message = JsonSerializer.Deserialize<InvoiceUploadedMessage>(args.Body.Span, JsonOptions);
            }
            catch (JsonException exception)
            {
                LogInvalidPayload(logger, exception);
                await channel.BasicAckAsync(args.DeliveryTag, multiple: false, cancellationToken: CancellationToken.None);
                return;
            }

            if (message is null || message.TenantId == Guid.Empty || message.InvoiceId == Guid.Empty)
            {
                LogMissingIds(logger);
                await channel.BasicAckAsync(args.DeliveryTag, multiple: false, cancellationToken: CancellationToken.None);
                return;
            }

            int attempt = DeliveryAttempt(args);
            var tenantId = new TenantId(message.TenantId);
            var invoiceId = new InvoiceId(message.InvoiceId);

            try
            {
                using (MessageTenantContext.Use(tenantId))
                await using (AsyncServiceScope scope = scopeFactory.CreateAsyncScope())
                {
                    InvoiceProcessingService processor =
                        scope.ServiceProvider.GetRequiredService<InvoiceProcessingService>();

                    if (attempt >= settings.MaxDeliveryAttempts)
                    {
                        LogGivingUp(logger, invoiceId.Value, attempt);
                        await processor.FailAfterRetriesAsync(
                            invoiceId, "Extraction failed after repeated attempts", stoppingToken);
                    }
                    else
                    {
                        await processor.ProcessAsync(invoiceId, stoppingToken);
                    }
                }

                await channel.BasicAckAsync(args.DeliveryTag, multiple: false, cancellationToken: CancellationToken.None);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                LogProcessingFailed(logger, exception, invoiceId.Value, attempt);

                bool requeue = attempt < settings.MaxDeliveryAttempts;
                await channel.BasicNackAsync(
                    args.DeliveryTag, multiple: false, requeue: requeue, cancellationToken: CancellationToken.None);

                if (!requeue)
                {
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
                }
            }
        }
    }

    private static int DeliveryAttempt(BasicDeliverEventArgs args)
    {
        if (args.BasicProperties.Headers is { } headers
            && headers.TryGetValue("x-delivery-count", out object? raw))
        {
            return raw switch
            {
                byte b => b + 1,
                sbyte sb => sb + 1,
                int i => i + 1,
                long l => (int)l + 1,
                byte[] bytes when bytes.Length > 0 => bytes[0] + 1,
                _ => args.Redelivered ? 2 : 1,
            };
        }

        return args.Redelivered ? 2 : 1;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "RabbitMq:Host is not configured; invoice processing consumer is idle")]
    private static partial void LogIdle(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Consuming queue {Queue}")]
    private static partial void LogConsuming(ILogger logger, string queue);

    [LoggerMessage(Level = LogLevel.Error, Message = "Invalid invoice.uploaded payload; acknowledging to drop poison message")]
    private static partial void LogInvalidPayload(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "invoice.uploaded payload missing ids; acknowledging to drop poison message")]
    private static partial void LogMissingIds(ILogger logger);

    [LoggerMessage(Level = LogLevel.Error, Message = "Giving up on invoice {InvoiceId} after {Attempt} attempts")]
    private static partial void LogGivingUp(ILogger logger, Guid invoiceId, int attempt);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed processing invoice {InvoiceId} (attempt {Attempt})")]
    private static partial void LogProcessingFailed(ILogger logger, Exception exception, Guid invoiceId, int attempt);

    [LoggerMessage(Level = LogLevel.Error, Message = "Could not mark invoice {InvoiceId} as extraction failed")]
    private static partial void LogFailMarkFailed(ILogger logger, Exception exception, Guid invoiceId);
}
