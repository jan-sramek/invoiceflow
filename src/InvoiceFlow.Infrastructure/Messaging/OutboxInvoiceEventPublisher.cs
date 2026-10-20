using System.Text.Json;
using InvoiceFlow.Application.Abstractions.Messaging;
using InvoiceFlow.Domain.Invoices;
using InvoiceFlow.Domain.Tenants;
using InvoiceFlow.Infrastructure.Persistence.Tenant;

namespace InvoiceFlow.Infrastructure.Messaging;

/// <summary>
/// Writes <c>invoice.uploaded</c> into <see cref="OutboxMessage"/> in the current tenant unit of work.
/// </summary>
internal sealed class OutboxInvoiceEventPublisher(TenantDbContext db, TimeProvider time) : IInvoiceEventPublisher
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public Task PublishUploadedAsync(TenantId tenantId, InvoiceId invoiceId, CancellationToken cancellationToken)
    {
        DateTimeOffset now = time.GetUtcNow();
        var id = Guid.CreateVersion7(now);
        string payload = JsonSerializer.Serialize(new InvoiceUploadedMessage(tenantId.Value, invoiceId.Value), JsonOptions);

        db.OutboxMessages.Add(new OutboxMessage
        {
            Id = id,
            Type = OutboxMessage.InvoiceUploadedType,
            Payload = payload,
            Headers = MessagingTraceContext.CaptureJson(),
            CreatedAt = now,
        });

        return Task.CompletedTask;
    }
}
