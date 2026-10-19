using System.Text.Json;
using InvoiceFlow.Application.Abstractions.Messaging;
using InvoiceFlow.Domain.Invoices;
using InvoiceFlow.Domain.Tenants;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace InvoiceFlow.Infrastructure.Messaging;

internal sealed partial class RabbitMqInvoiceEventPublisher(
    IOptions<RabbitMqOptions> options,
    ILogger<RabbitMqInvoiceEventPublisher> logger) : IInvoiceEventPublisher, IAsyncDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly SemaphoreSlim _gate = new(1, 1);
    private IConnection? _connection;
    private IChannel? _channel;

    public async Task PublishUploadedAsync(TenantId tenantId, InvoiceId invoiceId, CancellationToken cancellationToken)
    {
        byte[] body = JsonSerializer.SerializeToUtf8Bytes(
            new InvoiceUploadedMessage(tenantId.Value, invoiceId.Value), JsonOptions);

        await _gate.WaitAsync(cancellationToken);
        try
        {
            IChannel channel = await EnsureChannelAsync(cancellationToken);
            var properties = new BasicProperties { ContentType = "application/json", DeliveryMode = DeliveryModes.Persistent };
            await channel.BasicPublishAsync(
                exchange: InvoiceFlowTopology.EventsExchange,
                routingKey: InvoiceFlowTopology.UploadedRoutingKey,
                mandatory: false,
                basicProperties: properties,
                body: body,
                cancellationToken: cancellationToken);

            LogPublished(logger, tenantId.Value, invoiceId.Value);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<IChannel> EnsureChannelAsync(CancellationToken cancellationToken)
    {
        if (_channel is { IsOpen: true })
        {
            return _channel;
        }

        await DisposeConnectionAsync();

        RabbitMqOptions settings = options.Value;
        var factory = new ConnectionFactory
        {
            HostName = settings.Host,
            Port = settings.Port,
            VirtualHost = settings.VirtualHost,
            UserName = settings.UserName,
            Password = settings.Password,
        };

        _connection = await factory.CreateConnectionAsync(cancellationToken);
        _channel = await _connection.CreateChannelAsync(cancellationToken: cancellationToken);
        return _channel;
    }

    public async ValueTask DisposeAsync()
    {
        await _gate.WaitAsync();
        try
        {
            await DisposeConnectionAsync();
        }
        finally
        {
            _gate.Release();
            _gate.Dispose();
        }
    }

    private async Task DisposeConnectionAsync()
    {
        if (_channel is not null)
        {
            await _channel.DisposeAsync();
            _channel = null;
        }

        if (_connection is not null)
        {
            await _connection.DisposeAsync();
            _connection = null;
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Published invoice.uploaded for tenant {TenantId} invoice {InvoiceId}")]
    private static partial void LogPublished(ILogger logger, Guid tenantId, Guid invoiceId);
}
