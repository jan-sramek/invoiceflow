using InvoiceFlow.Domain.Invoices;

namespace InvoiceFlow.Application.Abstractions.Extraction;

/// <summary>
/// Extracts structured invoice fields from a PDF. Implementations must not throw for unreadable
/// content; return empty fields so the worker can send the invoice to review.
/// </summary>
public interface IInvoiceExtractor
{
    Task<InvoiceFields> ExtractAsync(Stream pdf, CancellationToken cancellationToken);
}
