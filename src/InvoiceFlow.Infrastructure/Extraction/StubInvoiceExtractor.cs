using InvoiceFlow.Application.Abstractions.Extraction;
using InvoiceFlow.Domain.Invoices;

namespace InvoiceFlow.Infrastructure.Extraction;

/// <summary>
/// Returns empty fields so the worker sends the invoice to review. Used when Ollama is not configured.
/// </summary>
internal sealed class StubInvoiceExtractor : IInvoiceExtractor
{
    public Task<ExtractionResult> ExtractAsync(Stream pdf, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pdf);
        return Task.FromResult<ExtractionResult>(new ExtractionResult.Succeeded(InvoiceFields.Empty));
    }
}
