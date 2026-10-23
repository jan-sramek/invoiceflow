using InvoiceFlow.Domain.Common;
using InvoiceFlow.Domain.Invoices;
using InvoiceFlow.Infrastructure.Extraction;

namespace InvoiceFlow.Worker.Tests;

public class ExtractedFieldAssessorTests
{
    private static readonly DateOnly Today = new(2026, 10, 21);

    [Fact]
    public void Assess_ValueInText_IsValid()
    {
        var fields = new InvoiceFields
        {
            SupplierName = SupplierName.Create("Acme s.r.o."),
            InvoiceNumber = InvoiceNumber.Create("FV-100"),
            InvoiceDate = new DateOnly(2026, 9, 1),
            Currency = CurrencyCode.Create("EUR"),
            TotalAmount = 121.00m,
        };
        const string Source = "Supplier Acme s.r.o. Invoice FV-100 dated 2026-09-01 Total EUR 121.00";

        InvoiceFieldStatuses statuses = ExtractedFieldAssessor.Assess(fields, Source, Today);

        statuses.SupplierName.ShouldBe(FieldStatus.Valid);
        statuses.InvoiceNumber.ShouldBe(FieldStatus.Valid);
        statuses.InvoiceDate.ShouldBe(FieldStatus.Valid);
        statuses.Currency.ShouldBe(FieldStatus.Valid);
        statuses.TotalAmount.ShouldBe(FieldStatus.Valid);
        statuses.DueDate.ShouldBe(FieldStatus.Missing);
    }

    [Fact]
    public void Assess_ValueMissingFromText_IsUnverified()
    {
        var fields = new InvoiceFields
        {
            SupplierName = SupplierName.Create("Ghost Corp"),
            InvoiceNumber = InvoiceNumber.Create("X-1"),
            InvoiceDate = new DateOnly(2026, 9, 1),
            Currency = CurrencyCode.Create("EUR"),
            TotalAmount = 10.00m,
        };

        InvoiceFieldStatuses statuses = ExtractedFieldAssessor.Assess(fields, "Completely unrelated PDF text", Today);

        statuses.SupplierName.ShouldBe(FieldStatus.Unverified);
        statuses.TotalAmount.ShouldBe(FieldStatus.Unverified);
    }

    [Fact]
    public void Assess_TotalMismatch_IsInvalid()
    {
        var fields = new InvoiceFields
        {
            SupplierName = SupplierName.Create("Acme"),
            InvoiceNumber = InvoiceNumber.Create("1"),
            InvoiceDate = new DateOnly(2026, 9, 1),
            Currency = CurrencyCode.Create("EUR"),
            Subtotal = 100m,
            TaxAmount = 21m,
            TotalAmount = 999m,
        };
        const string Source = "Acme 1 2026-09-01 EUR 100 21 999";

        InvoiceFieldStatuses statuses = ExtractedFieldAssessor.Assess(fields, Source, Today);

        statuses.TotalAmount.ShouldBe(FieldStatus.Invalid);
    }

    [Fact]
    public void Assess_EnglishMonthNameAndDollarSign_AreValid()
    {
        var fields = new InvoiceFields
        {
            SupplierName = SupplierName.Create("DEMO"),
            InvoiceNumber = InvoiceNumber.Create("INV-3337"),
            InvoiceDate = new DateOnly(2016, 1, 25),
            DueDate = new DateOnly(2016, 1, 31),
            Currency = CurrencyCode.Create("USD"),
            Subtotal = 85.00m,
            TaxAmount = 8.50m,
            TotalAmount = 93.50m,
        };
        const string Source =
            "Invoice DateJanuary 25, 2016Due DateJanuary 31, 2016Total Due$93.50 Sub Total$85.00Tax$8.50Total$93.50";

        InvoiceFieldStatuses statuses = ExtractedFieldAssessor.Assess(fields, Source, Today);

        statuses.InvoiceDate.ShouldBe(FieldStatus.Valid);
        statuses.DueDate.ShouldBe(FieldStatus.Valid);
        statuses.Currency.ShouldBe(FieldStatus.Valid);
        statuses.Subtotal.ShouldBe(FieldStatus.Valid);
        statuses.TaxAmount.ShouldBe(FieldStatus.Valid);
        statuses.TotalAmount.ShouldBe(FieldStatus.Valid);
    }
}

public class DocumentFieldHintsTests
{
    [Fact]
    public void Enrich_SlicedInvoicesSample_CorrectsSubtotalTaxTotal()
    {
        // Real PdfPig text: line-item "Sub Total1.00" (qty) appears before summary "Sub Total$85.00".
        const string Source =
            "Invoice DateJanuary 25, 2016Due DateJanuary 31, 2016Total Due$93.50"
            + "Hrs/QtyServiceRate/PriceAdjustSub Total1.00Web DesignThis is a sample description..."
            + "$85.000.00%$85.00Sub Total$85.00Tax$8.50Total$93.50";

        var wrong = new InvoiceFields
        {
            Subtotal = 93.50m,
            TaxAmount = 8.50m,
            TotalAmount = 93.50m,
            Currency = null,
        };

        InvoiceFields enriched = DocumentFieldHints.Enrich(wrong, Source);

        enriched.Subtotal.ShouldBe(85.00m);
        enriched.TaxAmount.ShouldBe(8.50m);
        enriched.TotalAmount.ShouldBe(93.50m);
        enriched.Currency!.Value.ShouldBe("USD");
    }
}
