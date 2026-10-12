using InvoiceFlow.Domain.Common;

namespace InvoiceFlow.Domain.Invoices;

public sealed record InvoiceNumber
{
    public const int MaxLength = 50;

    private InvoiceNumber(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static InvoiceNumber Create(string? value) => new(Text.Require(value, "invoiceNumber", MaxLength));

    public override string ToString() => Value;
}
