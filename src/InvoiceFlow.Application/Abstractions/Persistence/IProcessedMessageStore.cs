namespace InvoiceFlow.Application.Abstractions.Persistence;

/// <summary>
/// Records RabbitMQ message ids that have already driven an invoice outcome (ADR-0006).
/// </summary>
public interface IProcessedMessageStore
{
    Task<bool> ExistsAsync(Guid messageId, CancellationToken cancellationToken);

    void Add(Guid messageId, DateTimeOffset processedAt);
}
