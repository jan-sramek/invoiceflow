using InvoiceFlow.Application.Abstractions.Extraction;
using InvoiceFlow.Domain.Invoices;
using InvoiceFlow.Infrastructure.Extraction;

namespace InvoiceFlow.Worker.Tests;

public class InvoiceExtractionJsonTests
{
    [Fact]
    public void Parse_ValidJson_MapsFields()
    {
        InvoiceFields fields = InvoiceExtractionJson.Parse("""
            {
              "supplierName": "Acme s.r.o.",
              "invoiceNumber": "FV-1",
              "invoiceDate": "2026-10-01",
              "dueDate": "2026-10-31",
              "currency": "CZK",
              "subtotal": 100,
              "taxAmount": 21,
              "totalAmount": 121,
              "purchaseOrderReference": "PO-1"
            }
            """);

        fields.SupplierName!.Value.ShouldBe("Acme s.r.o.");
        fields.InvoiceNumber!.Value.ShouldBe("FV-1");
        fields.InvoiceDate.ShouldBe(new DateOnly(2026, 10, 1));
        fields.Currency!.Value.ShouldBe("CZK");
        fields.TotalAmount.ShouldBe(121m);
        fields.PurchaseOrderReference!.Value.ShouldBe("PO-1");
    }

    [Fact]
    public void Parse_MarkdownWrappedJson_ExtractsObject()
    {
        InvoiceFields fields = InvoiceExtractionJson.Parse("""
            Here you go:
            ```json
            { "supplierName": "Beta", "totalAmount": "10.00", "currency": "EUR" }
            ```
            """);

        fields.SupplierName!.Value.ShouldBe("Beta");
        fields.TotalAmount.ShouldBe(10.00m);
        fields.Currency!.Value.ShouldBe("EUR");
    }

    [Fact]
    public void Parse_InvalidOrEmpty_ReturnsEmpty()
    {
        InvoiceExtractionJson.Parse(null).ShouldBe(InvoiceFields.Empty);
        InvoiceExtractionJson.Parse("").ShouldBe(InvoiceFields.Empty);
        InvoiceExtractionJson.Parse("not json").ShouldBe(InvoiceFields.Empty);
        InvoiceExtractionJson.Parse("""{ "currency": "NOPE" }""").Currency.ShouldBeNull();
    }

    [Fact]
    public async Task StubExtractor_ReturnsSucceededEmpty()
    {
        await using var pdf = new MemoryStream("%PDF"u8.ToArray());
        ExtractionResult result = await new StubInvoiceExtractor().ExtractAsync(pdf, TestContext.Current.CancellationToken);
        result.ShouldBe(new ExtractionResult.Succeeded(InvoiceFields.Empty, InvoiceFieldStatuses.Empty));
    }
}
