using InvoiceFlow.Application.Errors;
using InvoiceFlow.Domain.Common;
using InvoiceFlow.Domain.Errors;
using InvoiceFlow.Domain.Invoices;

namespace InvoiceFlow.Application.Invoices.UpdateFields;

/// <summary>
/// The complete set of editable fields as the client sends it. A missing or blank value clears the field.
/// </summary>
public sealed record InvoiceFieldsInput(
    string? SupplierName,
    string? InvoiceNumber,
    DateOnly? InvoiceDate,
    DateOnly? DueDate,
    string? Currency,
    decimal? Subtotal,
    decimal? TaxAmount,
    decimal? TotalAmount,
    string? PurchaseOrderReference)
{
    /// <summary>
    /// Builds the domain fields with the domain's own rules and reports every field that breaks one,
    /// instead of stopping at the first.
    /// </summary>
    internal InvoiceFields ToFields(out IReadOnlyList<RequestFieldError> errors)
    {
        List<RequestFieldError> found = [];

        var fields = new InvoiceFields
        {
            SupplierName = Text(nameof(SupplierName), SupplierName, Domain.Invoices.SupplierName.Create, found),
            InvoiceNumber = Text(nameof(InvoiceNumber), InvoiceNumber, Domain.Invoices.InvoiceNumber.Create, found),
            InvoiceDate = InvoiceDate,
            DueDate = DueDate,
            Currency = Text(nameof(Currency), Currency, CurrencyCode.Create, found),
            Subtotal = Amount(nameof(Subtotal), Subtotal, value => InvoiceFields.Empty with { Subtotal = value }, found),
            TaxAmount = Amount(nameof(TaxAmount), TaxAmount, value => InvoiceFields.Empty with { TaxAmount = value }, found),
            TotalAmount = Amount(nameof(TotalAmount), TotalAmount, value => InvoiceFields.Empty with { TotalAmount = value }, found),
            PurchaseOrderReference = Text(
                nameof(PurchaseOrderReference), PurchaseOrderReference, Domain.Invoices.PurchaseOrderReference.Create, found),
        };

        errors = found;
        return fields;
    }

    private static T? Text<T>(string property, string? value, Func<string, T> create, List<RequestFieldError> errors)
        where T : class =>
        string.IsNullOrWhiteSpace(value) ? null : Check(property, () => create(value), errors);

    // Amounts are checked by the InvoiceFields setters, so each one is tried on an otherwise empty set.
    private static decimal? Amount(
        string property, decimal? value, Func<decimal?, InvoiceFields> create, List<RequestFieldError> errors) =>
        Check<object>(property, () => create(value), errors) is null ? null : value;

    private static T? Check<T>(string property, Func<T> create, List<RequestFieldError> errors)
        where T : class
    {
        try
        {
            return create();
        }
        catch (DomainValidationException exception)
        {
            errors.Add(new RequestFieldError(property, exception.Code, exception.Params));
            return null;
        }
    }
}
