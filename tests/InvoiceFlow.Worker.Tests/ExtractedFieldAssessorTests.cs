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
    public void DetectLanguage_CzechDiacritics_ReturnsCs()
    {
        ExtractedFieldAssessor.DetectLanguage("Faktura číslo dodavatel částka Kč", null).ShouldBe("cs");
    }
}
