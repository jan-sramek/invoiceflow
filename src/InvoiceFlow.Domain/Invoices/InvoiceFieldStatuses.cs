namespace InvoiceFlow.Domain.Invoices;

/// <summary>
/// Per-field assessment after extraction or human edit. Defaults to <see cref="FieldStatus.Missing"/>.
/// </summary>
public sealed record InvoiceFieldStatuses
{
    public static readonly InvoiceFieldStatuses Empty = new();

    public FieldStatus SupplierName { get; init; }

    public FieldStatus InvoiceNumber { get; init; }

    public FieldStatus InvoiceDate { get; init; }

    public FieldStatus DueDate { get; init; }

    public FieldStatus Currency { get; init; }

    public FieldStatus Subtotal { get; init; }

    public FieldStatus TaxAmount { get; init; }

    public FieldStatus TotalAmount { get; init; }

    public FieldStatus PurchaseOrderReference { get; init; }

    public FieldStatus Of(InvoiceField field) => field switch
    {
        InvoiceField.SupplierName => SupplierName,
        InvoiceField.InvoiceNumber => InvoiceNumber,
        InvoiceField.InvoiceDate => InvoiceDate,
        InvoiceField.DueDate => DueDate,
        InvoiceField.Currency => Currency,
        InvoiceField.Subtotal => Subtotal,
        InvoiceField.TaxAmount => TaxAmount,
        InvoiceField.TotalAmount => TotalAmount,
        InvoiceField.PurchaseOrderReference => PurchaseOrderReference,
        _ => FieldStatus.Missing,
    };

    /// <summary>
    /// After a human corrects fields, changed values are trusted as <see cref="FieldStatus.Valid"/>.
    /// </summary>
    public InvoiceFieldStatuses AfterHumanEdits(InvoiceFields before, InvoiceFields after)
    {
        FieldStatus KeepOrValid(InvoiceField field, bool changed) =>
            changed ? FieldStatus.Valid : Of(field);

        IReadOnlyList<FieldChange> changes = after.ChangesFrom(before);
        var changed = changes.Select(c => c.Field).ToHashSet();

        return new InvoiceFieldStatuses
        {
            SupplierName = KeepOrValid(InvoiceField.SupplierName, changed.Contains(InvoiceField.SupplierName)),
            InvoiceNumber = KeepOrValid(InvoiceField.InvoiceNumber, changed.Contains(InvoiceField.InvoiceNumber)),
            InvoiceDate = KeepOrValid(InvoiceField.InvoiceDate, changed.Contains(InvoiceField.InvoiceDate)),
            DueDate = KeepOrValid(InvoiceField.DueDate, changed.Contains(InvoiceField.DueDate)),
            Currency = KeepOrValid(InvoiceField.Currency, changed.Contains(InvoiceField.Currency)),
            Subtotal = KeepOrValid(InvoiceField.Subtotal, changed.Contains(InvoiceField.Subtotal)),
            TaxAmount = KeepOrValid(InvoiceField.TaxAmount, changed.Contains(InvoiceField.TaxAmount)),
            TotalAmount = KeepOrValid(InvoiceField.TotalAmount, changed.Contains(InvoiceField.TotalAmount)),
            PurchaseOrderReference = KeepOrValid(
                InvoiceField.PurchaseOrderReference, changed.Contains(InvoiceField.PurchaseOrderReference)),
        };
    }
}
