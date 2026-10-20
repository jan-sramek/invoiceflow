using InvoiceFlow.Application.Abstractions.Persistence;
using InvoiceFlow.Infrastructure.Persistence.Tenant;
using Microsoft.EntityFrameworkCore;

namespace InvoiceFlow.Infrastructure.Messaging;

internal sealed class ProcessedMessageStore(TenantDbContext db) : IProcessedMessageStore
{
    public Task<bool> ExistsAsync(Guid messageId, CancellationToken cancellationToken) =>
        db.ProcessedMessages.AnyAsync(m => m.MessageId == messageId, cancellationToken);

    public void Add(Guid messageId, DateTimeOffset processedAt) =>
        db.ProcessedMessages.Add(new ProcessedMessage
        {
            MessageId = messageId,
            ProcessedAt = processedAt,
        });
}
