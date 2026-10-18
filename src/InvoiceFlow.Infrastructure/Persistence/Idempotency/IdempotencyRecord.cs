namespace InvoiceFlow.Infrastructure.Persistence.Idempotency;

internal sealed class IdempotencyRecord
{
    public required string Key { get; init; }

    public required string UserId { get; init; }

    public required string RequestHash { get; init; }

    public Guid? InvoiceId { get; set; }

    public required DateTimeOffset CreatedAt { get; init; }
}
