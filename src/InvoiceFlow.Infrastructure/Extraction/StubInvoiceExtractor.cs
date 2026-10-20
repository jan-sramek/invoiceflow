using InvoiceFlow.Application.Abstractions.Extraction;
using Microsoft.Extensions.Options;

namespace InvoiceFlow.Infrastructure.Extraction;

/// <summary>
/// Always succeeds with empty fields. Convenience for unit tests that do not need options.
/// </summary>
internal sealed class StubInvoiceExtractor : IInvoiceExtractor
{
    private readonly FakeInvoiceExtractor _inner = new(Options.Create(new FakeExtractorOptions()));

    public Task<ExtractionResult> ExtractAsync(Stream pdf, CancellationToken cancellationToken) =>
        _inner.ExtractAsync(pdf, cancellationToken);
}
