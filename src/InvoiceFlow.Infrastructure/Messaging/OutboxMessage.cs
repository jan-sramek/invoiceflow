namespace InvoiceFlow.Infrastructure.Messaging;

internal sealed class OutboxMessage
{
    public const string InvoiceUploadedType = "invoice.uploaded";

    public required Guid Id { get; init; }

    public required string Type { get; init; }

    public required string Payload { get; init; }

    public string? Headers { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset? PublishedAt { get; set; }

    public int Attempts { get; set; }
}
