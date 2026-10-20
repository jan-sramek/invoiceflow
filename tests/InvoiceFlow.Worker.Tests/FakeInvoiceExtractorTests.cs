using InvoiceFlow.Application.Abstractions.Extraction;
using InvoiceFlow.Application.Errors;
using InvoiceFlow.Infrastructure.Extraction;
using Microsoft.Extensions.Options;

namespace InvoiceFlow.Worker.Tests;

public class FakeInvoiceExtractorTests
{
    [Fact]
    public async Task ExtractAsync_Default_SucceedsWithEmptyFields()
    {
        var extractor = new FakeInvoiceExtractor(Options.Create(new FakeExtractorOptions()));
        await using var pdf = new MemoryStream("%PDF"u8.ToArray());

        ExtractionResult result = await extractor.ExtractAsync(pdf, TestContext.Current.CancellationToken);

        result.ShouldBeOfType<ExtractionResult.Succeeded>();
    }

    [Fact]
    public async Task ExtractAsync_FailureRateOne_Technical_Throws()
    {
        var extractor = new FakeInvoiceExtractor(Options.Create(new FakeExtractorOptions
        {
            FailureRate = 1,
            FailureMode = FakeExtractorFailureMode.Technical,
        }));
        await using var pdf = new MemoryStream("%PDF"u8.ToArray());

        await Should.ThrowAsync<HttpRequestException>(() =>
            extractor.ExtractAsync(pdf, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ExtractAsync_FailureRateOne_Business_ReturnsFailed()
    {
        var extractor = new FakeInvoiceExtractor(Options.Create(new FakeExtractorOptions
        {
            FailureRate = 1,
            FailureMode = FakeExtractorFailureMode.Business,
            BusinessFailureCode = ApplicationErrors.Extraction.FakeFailed,
        }));
        await using var pdf = new MemoryStream("%PDF"u8.ToArray());

        ExtractionResult result = await extractor.ExtractAsync(pdf, TestContext.Current.CancellationToken);

        result.ShouldBe(new ExtractionResult.Failed(ApplicationErrors.Extraction.FakeFailed));
    }

    [Fact]
    public async Task ExtractAsync_Delay_HonoursCancellation()
    {
        var extractor = new FakeInvoiceExtractor(Options.Create(new FakeExtractorOptions
        {
            Delay = TimeSpan.FromSeconds(30),
        }));
        await using var pdf = new MemoryStream("%PDF"u8.ToArray());
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        await Should.ThrowAsync<OperationCanceledException>(() =>
            extractor.ExtractAsync(pdf, cts.Token));
    }
}
