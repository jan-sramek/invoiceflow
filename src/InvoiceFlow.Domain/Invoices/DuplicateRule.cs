namespace InvoiceFlow.Domain.Invoices;

/// <summary>
/// Why an invoice was flagged as a possible duplicate. Values are stored and returned by the API.
/// </summary>
public static class DuplicateRule
{
    public const string ExactFile = "exact_file";
}
