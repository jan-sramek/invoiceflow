using System.Text;
using InvoiceFlow.Application.Errors;
using Microsoft.Extensions.Options;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace InvoiceFlow.Infrastructure.Extraction;

/// <summary>
/// Extracts a text layer from a PDF. Scanned (image-only) documents yield <see cref="ApplicationErrors.Extraction.NoTextLayer"/>.
/// </summary>
internal sealed class PdfPigTextExtractor(IOptions<DocumentExtractionOptions> options)
{
    public PdfTextResult Extract(Stream pdf)
    {
        ArgumentNullException.ThrowIfNull(pdf);

        DocumentExtractionOptions settings = options.Value;
        try
        {
            using var document = PdfDocument.Open(pdf);
            if (document.NumberOfPages > settings.MaxPages)
            {
                return new PdfTextResult.Failed(ApplicationErrors.Extraction.TooManyPages);
            }

            var text = new StringBuilder();
            foreach (Page page in document.GetPages())
            {
                if (!string.IsNullOrWhiteSpace(page.Text))
                {
                    if (text.Length > 0)
                    {
                        text.AppendLine();
                    }

                    text.Append(page.Text.Trim());
                }
            }

            string cleaned = text.ToString().Trim();
            if (cleaned.Length == 0)
            {
                return new PdfTextResult.Failed(ApplicationErrors.Extraction.NoTextLayer);
            }

            if (cleaned.Length > settings.MaxTextChars)
            {
                cleaned = cleaned[..settings.MaxTextChars];
            }

            return new PdfTextResult.Succeeded(cleaned);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return new PdfTextResult.Failed(ApplicationErrors.Extraction.PdfUnreadable);
        }
    }
}
