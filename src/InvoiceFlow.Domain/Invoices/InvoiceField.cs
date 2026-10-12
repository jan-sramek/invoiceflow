namespace InvoiceFlow.Domain.Invoices;

public enum InvoiceField
{
    SupplierName,
    InvoiceNumber,
    InvoiceDate,
    DueDate,
    Currency,
    Subtotal,
    TaxAmount,
    TotalAmount,
    PurchaseOrderReference,
}

public sealed record FieldChange(InvoiceField Field, string? OldValue, string? NewValue);

public sealed record FieldIssue(InvoiceField Field, string Code);
