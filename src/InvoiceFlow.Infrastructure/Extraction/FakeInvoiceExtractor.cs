using InvoiceFlow.Application.Abstractions.Extraction;
using InvoiceFlow.Application.Errors;
using InvoiceFlow.Domain.Invoices;
using Microsoft.Extensions.Options;

namespace InvoiceFlow.Infrastructure.Extraction;

/// <summary>
/// Configurable fake used when Ollama is not configured: optional delay and probabilistic failures.
/// </summary>
internal sealed class FakeInvoiceExtractor(IOptions<FakeExtractorOptions> options) : IInvoiceExtractor
{
    public async Task<ExtractionResult> ExtractAsync(Stream pdf, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pdf);

        FakeExtractorOptions settings = options.Value;
        if (settings.Delay > TimeSpan.Zero)
        {
            await Task.Delay(settings.Delay, cancellationToken);
        }

        if (settings.FailureRate > 0 && Random.Shared.NextDouble() < settings.FailureRate)
        {
            if (settings.FailureMode == FakeExtractorFailureMode.Business)
            {
                string code = string.IsNullOrWhiteSpace(settings.BusinessFailureCode)
                    ? ApplicationErrors.Extraction.FakeFailed
                    : settings.BusinessFailureCode.Trim();
                return new ExtractionResult.Failed(code);
            }

            throw new HttpRequestException("Simulated technical extraction failure.");
        }

        return new ExtractionResult.Succeeded(InvoiceFields.Empty);
    }
}
