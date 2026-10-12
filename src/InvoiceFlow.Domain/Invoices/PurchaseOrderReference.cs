using InvoiceFlow.Domain.Common;

namespace InvoiceFlow.Domain.Invoices;

public sealed record PurchaseOrderReference
{
    public const int MaxLength = 50;

    private PurchaseOrderReference(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static PurchaseOrderReference Create(string? value) =>
        new(Text.Require(value, "purchaseOrderReference", MaxLength));

    public override string ToString() => Value;
}
