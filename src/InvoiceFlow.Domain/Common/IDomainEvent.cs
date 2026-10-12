namespace InvoiceFlow.Domain.Common;

public interface IDomainEvent
{
    Actor Actor { get; }

    DateTimeOffset OccurredAt { get; }
}
