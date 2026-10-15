using System.Diagnostics.CodeAnalysis;

namespace InvoiceFlow.Application.Invoices.ListInvoices;

public enum InvoiceSortField
{
    UploadedAt,
    InvoiceDate,
    DueDate,
    TotalAmount,
    Status,
    SupplierName,
}

/// <summary>
/// Written as the field's name, with a leading <c>-</c> for descending order: <c>dueDate</c>, <c>-totalAmount</c>.
/// </summary>
public sealed record InvoiceSort(InvoiceSortField Field, bool Descending)
{
    public static readonly InvoiceSort Default = new(InvoiceSortField.UploadedAt, Descending: true);

    private static readonly Dictionary<string, InvoiceSortField> FieldsByName = Enum.GetValues<InvoiceSortField>()
        .ToDictionary(field => char.ToLowerInvariant(field.ToString()[0]) + field.ToString()[1..]);

    public static IReadOnlyList<string> Names { get; } = [.. FieldsByName.Keys];

    public static bool TryParse(string? value, [NotNullWhen(true)] out InvoiceSort? sort)
    {
        if (string.IsNullOrEmpty(value))
        {
            sort = Default;
            return true;
        }

        bool descending = value.StartsWith('-');
        sort = FieldsByName.TryGetValue(descending ? value[1..] : value, out InvoiceSortField field)
            ? new InvoiceSort(field, descending)
            : null;
        return sort is not null;
    }
}
