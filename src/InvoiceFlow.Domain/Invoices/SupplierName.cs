using InvoiceFlow.Domain.Common;

namespace InvoiceFlow.Domain.Invoices;

public sealed record SupplierName
{
    public const int MaxLength = 200;

    private SupplierName(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static SupplierName Create(string? value) => new(Text.Require(value, "supplierName", MaxLength));

    public override string ToString() => Value;
}
