using InvoiceFlow.Application.Errors;
using InvoiceFlow.Infrastructure.Extraction;
using Microsoft.Extensions.Options;

namespace InvoiceFlow.Worker.Tests;

public class PdfPigTextExtractorTests
{
    private static PdfPigTextExtractor CreateExtractor(int maxPages = 50) =>
        new(Options.Create(new DocumentExtractionOptions
        {
            ServiceUrl = "http://127.0.0.1:11434",
            Model = "test",
            MaxPages = maxPages,
            MaxTextChars = 12_000,
        }));

    [Fact]
    public void Extract_TextPdf_ReturnsText()
    {
        byte[] pdf = MinimalPdf.WithText("Acme Invoice FV-100 Total 121.00");
        using var stream = new MemoryStream(pdf);

        PdfTextResult result = CreateExtractor().Extract(stream);

        PdfTextResult.Succeeded succeeded = result.ShouldBeOfType<PdfTextResult.Succeeded>();
        succeeded.Text.ShouldContain("Acme Invoice");
        succeeded.Text.ShouldContain("FV-100");
    }

    [Fact]
    public void Extract_EmptyContentPdf_ReturnsNoTextLayer()
    {
        byte[] pdf = MinimalPdf.EmptyPage();
        using var stream = new MemoryStream(pdf);

        PdfTextResult result = CreateExtractor().Extract(stream);

        result.ShouldBe(new PdfTextResult.Failed(ApplicationErrors.Extraction.NoTextLayer));
    }

    [Fact]
    public void Extract_GarbageBytes_ReturnsPdfUnreadable()
    {
        using var stream = new MemoryStream("%PDF-not-a-real-file"u8.ToArray());

        PdfTextResult result = CreateExtractor().Extract(stream);

        result.ShouldBe(new PdfTextResult.Failed(ApplicationErrors.Extraction.PdfUnreadable));
    }

    [Fact]
    public void Extract_TwoPagePdf_ExceedingLimit_Fails()
    {
        byte[] pdf = MinimalPdf.TwoPages("first", "second");
        using var stream = new MemoryStream(pdf);

        PdfTextResult result = CreateExtractor(maxPages: 1).Extract(stream);

        result.ShouldBe(new PdfTextResult.Failed(ApplicationErrors.Extraction.TooManyPages));
    }
}
