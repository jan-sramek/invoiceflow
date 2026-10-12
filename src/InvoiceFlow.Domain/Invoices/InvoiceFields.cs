using System.Globalization;
using InvoiceFlow.Domain.Common;
using InvoiceFlow.Domain.Errors;

namespace InvoiceFlow.Domain.Invoices;

/// <summary>
/// The editable fields. Each one is optional while editing; <see cref="FindIssues"/> lists what blocks submission.
/// </summary>
public sealed record InvoiceFields
{
    public const decimal TotalTolerance = 0.01m;

    public static readonly InvoiceFields Empty = new();

    public SupplierName? SupplierName { get; init; }

    public InvoiceNumber? InvoiceNumber { get; init; }

    public DateOnly? InvoiceDate { get; init; }

    public DateOnly? DueDate { get; init; }

    public CurrencyCode? Currency { get; init; }

    public decimal? Subtotal { get; init => field = ValidAmount(value); }

    public decimal? TaxAmount { get; init => field = ValidAmount(value); }

    public decimal? TotalAmount { get; init => field = ValidAmount(value); }

    public PurchaseOrderReference? PurchaseOrderReference { get; init; }

    /// <param name="today">The latest current date in any time zone.</param>
    public IReadOnlyList<FieldIssue> FindIssues(DateOnly today)
    {
        List<FieldIssue> issues = [];

        void RequirePresent(InvoiceField field, object? value)
        {
            if (value is null)
            {
                issues.Add(new FieldIssue(field, DomainErrors.Validation.Required));
            }
        }

        RequirePresent(InvoiceField.SupplierName, SupplierName);
        RequirePresent(InvoiceField.InvoiceNumber, InvoiceNumber);
        RequirePresent(InvoiceField.InvoiceDate, InvoiceDate);
        RequirePresent(InvoiceField.Currency, Currency);
        RequirePresent(InvoiceField.TotalAmount, TotalAmount);

        if (TotalAmount <= 0)
        {
            issues.Add(new FieldIssue(InvoiceField.TotalAmount, DomainErrors.Validation.MustBePositive));
        }
        else if (Subtotal is { } subtotal && TaxAmount is { } tax && TotalAmount is { } total
            && Math.Abs(subtotal + tax - total) > TotalTolerance)
        {
            issues.Add(new FieldIssue(InvoiceField.TotalAmount, DomainErrors.Validation.TotalMismatch));
        }

        if (InvoiceDate > today)
        {
            issues.Add(new FieldIssue(InvoiceField.InvoiceDate, DomainErrors.Validation.DateInFuture));
        }

        if (DueDate < InvoiceDate)
        {
            issues.Add(new FieldIssue(InvoiceField.DueDate, DomainErrors.Validation.DueDateBeforeInvoiceDate));
        }

        return issues;
    }

    public IReadOnlyList<FieldChange> ChangesFrom(InvoiceFields previous) =>
    [
        .. new FieldChange[]
        {
            new(InvoiceField.SupplierName, previous.SupplierName?.Value, SupplierName?.Value),
            new(InvoiceField.InvoiceNumber, previous.InvoiceNumber?.Value, InvoiceNumber?.Value),
            new(InvoiceField.InvoiceDate, Format(previous.InvoiceDate), Format(InvoiceDate)),
            new(InvoiceField.DueDate, Format(previous.DueDate), Format(DueDate)),
            new(InvoiceField.Currency, previous.Currency?.Value, Currency?.Value),
            new(InvoiceField.Subtotal, Format(previous.Subtotal), Format(Subtotal)),
            new(InvoiceField.TaxAmount, Format(previous.TaxAmount), Format(TaxAmount)),
            new(InvoiceField.TotalAmount, Format(previous.TotalAmount), Format(TotalAmount)),
            new(InvoiceField.PurchaseOrderReference, previous.PurchaseOrderReference?.Value, PurchaseOrderReference?.Value),
        }.Where(change => change.OldValue != change.NewValue),
    ];

    private static decimal? ValidAmount(decimal? amount) => amount is null ? null : Money.ValidAmount(amount.Value);

    private static string? Format(DateOnly? date) => date?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static string? Format(decimal? amount) => amount?.ToString("0.00", CultureInfo.InvariantCulture);
}
