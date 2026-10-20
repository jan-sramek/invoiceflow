namespace InvoiceFlow.Infrastructure.Messaging;

internal sealed class ProcessedMessage
{
    public required Guid MessageId { get; init; }

    public required DateTimeOffset ProcessedAt { get; init; }
}
