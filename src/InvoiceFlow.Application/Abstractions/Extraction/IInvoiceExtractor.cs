namespace InvoiceFlow.Application.Abstractions.Extraction;

/// <summary>
/// Extracts structured invoice fields from a PDF. Business failures (no text layer, unreadable PDF)
/// return <see cref="ExtractionResult.Failed"/>; technical failures (Ollama down) throw so the worker can retry.
/// </summary>
public interface IInvoiceExtractor
{
    Task<ExtractionResult> ExtractAsync(Stream pdf, CancellationToken cancellationToken);
}
