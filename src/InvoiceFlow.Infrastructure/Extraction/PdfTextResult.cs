namespace InvoiceFlow.Infrastructure.Extraction;

internal abstract record PdfTextResult
{
    private PdfTextResult()
    {
    }

    public sealed record Succeeded(string Text) : PdfTextResult;

    public sealed record Failed(string Code) : PdfTextResult;
}
