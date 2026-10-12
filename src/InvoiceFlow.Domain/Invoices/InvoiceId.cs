namespace InvoiceFlow.Domain.Invoices;

public readonly record struct InvoiceId(Guid Value)
{
    // Version 7 ids are ordered by creation time, which keeps primary key indexes compact.
    public static InvoiceId New(DateTimeOffset createdAt) => new(Guid.CreateVersion7(createdAt));

    public override string ToString() => Value.ToString();
}
