using InvoiceFlow.Domain.Invoices;

namespace InvoiceFlow.Domain.Tests.Invoices;

public class InvoiceDocumentTests
{
    private static readonly string Hash = new('0', InvoiceDocument.Sha256Length);

    [Theory]
    [InlineData("FV-2026-0117.pdf", "FV-2026-0117.pdf")]
    [InlineData("  faktura říjen.pdf  ", "faktura říjen.pdf")]
    [InlineData(@"C:\Users\clara\Desktop\invoice.pdf", "invoice.pdf")]
    [InlineData("../../etc/passwd", "passwd")]
    [InlineData("in\u0000voice\r\n.pdf", "invoice.pdf")]
    [InlineData("folder/", InvoiceDocument.DefaultFileName)]
    [InlineData(" ", InvoiceDocument.DefaultFileName)]
    [InlineData(null, InvoiceDocument.DefaultFileName)]
    public void Create_FileName_KeepsOnlyASafeLastSegment(string? fileName, string expected)
    {
        Create(fileName).FileName.ShouldBe(expected);
    }

    [Fact]
    public void Create_LongFileName_IsCutWithoutSplittingACharacter()
    {
        string fileName = new string('a', InvoiceDocument.MaxFileNameLength - 1) + "😀.pdf";

        string cut = Create(fileName).FileName;

        cut.ShouldBe(new string('a', InvoiceDocument.MaxFileNameLength - 1));
    }

    [Theory]
    [InlineData(0, "")]
    [InlineData(63, "")]
    [InlineData(64, "0")]
    [InlineData(63, "A")]
    [InlineData(63, "g")]
    public void Create_HashNotLowercaseHexSha256_Throws(int zeros, string suffix)
    {
        string sha256 = new string('0', zeros) + suffix;

        Should.Throw<ArgumentException>(() =>
            InvoiceDocument.Create(Guid.CreateVersion7(), "key", "a.pdf", 1, sha256));
    }

    [Fact]
    public void Create_LowercaseHexSha256_IsKept()
    {
        string sha256 = "0123456789abcdef" + new string('f', 48);

        InvoiceDocument.Create(Guid.CreateVersion7(), "key", "a.pdf", 1, sha256).Sha256.ShouldBe(sha256);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_SizeNotPositive_Throws(long sizeBytes)
    {
        Should.Throw<ArgumentOutOfRangeException>(() =>
            InvoiceDocument.Create(Guid.CreateVersion7(), "key", "a.pdf", sizeBytes, Hash));
    }

    [Fact]
    public void Create_BlankStorageKey_Throws()
    {
        Should.Throw<ArgumentException>(() => InvoiceDocument.Create(Guid.CreateVersion7(), " ", "a.pdf", 1, Hash));
    }

    private static InvoiceDocument Create(string? fileName) =>
        InvoiceDocument.Create(Guid.CreateVersion7(), "tenants/t/documents/d", fileName, 1, Hash);
}
