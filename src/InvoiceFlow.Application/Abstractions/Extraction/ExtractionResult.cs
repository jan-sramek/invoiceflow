using InvoiceFlow.Domain.Invoices;

namespace InvoiceFlow.Application.Abstractions.Extraction;

/// <summary>
/// Outcome of invoice field extraction. Business failures must not be retried; technical failures throw.
/// </summary>
public abstract record ExtractionResult
{
    private ExtractionResult()
    {
    }

    public sealed record Succeeded(
        InvoiceFields Fields,
        InvoiceFieldStatuses Statuses,
        string? DocumentLanguage = null) : ExtractionResult;

    /// <param name="Code">Stable catalogue code stored as the FailExtraction reason until failure columns exist.</param>
    public sealed record Failed(string Code) : ExtractionResult;
}
