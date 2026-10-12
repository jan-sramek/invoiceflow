using System.Globalization;
using InvoiceFlow.Domain.Errors;
using InvoiceFlow.Domain.Invoices;
using static InvoiceFlow.Domain.Tests.Invoices.TestInvoices;

namespace InvoiceFlow.Domain.Tests.Invoices;

public class InvoiceFieldsTests
{
    [Fact]
    public void FindIssues_ValidFields_ReturnsNone()
    {
        ValidFields.FindIssues(Today).ShouldBeEmpty();
    }

    [Fact]
    [Trait("Rule", "BR-02")]
    public void FindIssues_Empty_ListsEveryRequiredField()
    {
        InvoiceFields.Empty.FindIssues(Today).ShouldBe(
        [
            new FieldIssue(InvoiceField.SupplierName, DomainErrors.Validation.Required),
            new FieldIssue(InvoiceField.InvoiceNumber, DomainErrors.Validation.Required),
            new FieldIssue(InvoiceField.InvoiceDate, DomainErrors.Validation.Required),
            new FieldIssue(InvoiceField.Currency, DomainErrors.Validation.Required),
            new FieldIssue(InvoiceField.TotalAmount, DomainErrors.Validation.Required),
        ]);
    }

    [Fact]
    [Trait("Rule", "BR-02")]
    public void FindIssues_OptionalFieldsMissing_ReturnsNone()
    {
        InvoiceFields fields = ValidFields with
        {
            DueDate = null,
            Subtotal = null,
            TaxAmount = null,
            PurchaseOrderReference = null,
        };

        fields.FindIssues(Today).ShouldBeEmpty();
    }

    [Theory]
    [Trait("Rule", "BR-03")]
    [InlineData("0")]
    [InlineData("-1210.00")]
    public void FindIssues_TotalNotPositive_ReportsTotal(string total)
    {
        InvoiceFields fields = ValidFields with { Subtotal = null, TaxAmount = null, TotalAmount = decimal.Parse(total, CultureInfo.InvariantCulture) };

        fields.FindIssues(Today).ShouldBe(
            [new FieldIssue(InvoiceField.TotalAmount, DomainErrors.Validation.MustBePositive)]);
    }

    [Theory]
    [Trait("Rule", "BR-04")]
    [InlineData("1210.01")]
    [InlineData("1209.99")]
    public void FindIssues_SumWithinTolerance_ReturnsNone(string total)
    {
        InvoiceFields fields = ValidFields with { TotalAmount = decimal.Parse(total, CultureInfo.InvariantCulture) };

        fields.FindIssues(Today).ShouldBeEmpty();
    }

    [Theory]
    [Trait("Rule", "BR-04")]
    [InlineData("1210.02")]
    [InlineData("1209.98")]
    [InlineData("1000.00")]
    public void FindIssues_SumOutsideTolerance_ReportsTotal(string total)
    {
        InvoiceFields fields = ValidFields with { TotalAmount = decimal.Parse(total, CultureInfo.InvariantCulture) };

        fields.FindIssues(Today).ShouldBe(
            [new FieldIssue(InvoiceField.TotalAmount, DomainErrors.Validation.TotalMismatch)]);
    }

    [Fact]
    [Trait("Rule", "BR-04")]
    public void FindIssues_OnlySubtotalPresent_DoesNotCompareSum()
    {
        InvoiceFields fields = ValidFields with { TaxAmount = null, TotalAmount = 5000m };

        fields.FindIssues(Today).ShouldBeEmpty();
    }

    [Fact]
    [Trait("Rule", "BR-05")]
    public void FindIssues_DueDateBeforeInvoiceDate_ReportsDueDate()
    {
        InvoiceFields fields = ValidFields with { DueDate = new DateOnly(2026, 9, 30) };

        fields.FindIssues(Today).ShouldBe(
            [new FieldIssue(InvoiceField.DueDate, DomainErrors.Validation.DueDateBeforeInvoiceDate)]);
    }

    [Fact]
    [Trait("Rule", "BR-05")]
    public void FindIssues_DueDateOnInvoiceDate_ReturnsNone()
    {
        InvoiceFields fields = ValidFields with { DueDate = ValidFields.InvoiceDate };

        fields.FindIssues(Today).ShouldBeEmpty();
    }

    [Fact]
    [Trait("Rule", "BR-05")]
    public void FindIssues_InvoiceDateAfterToday_ReportsInvoiceDate()
    {
        InvoiceFields fields = ValidFields with { InvoiceDate = Today.AddDays(1), DueDate = null };

        fields.FindIssues(Today).ShouldBe(
            [new FieldIssue(InvoiceField.InvoiceDate, DomainErrors.Validation.DateInFuture)]);
    }

    [Fact]
    [Trait("Rule", "BR-05")]
    public void FindIssues_InvoiceDateToday_ReturnsNone()
    {
        InvoiceFields fields = ValidFields with { InvoiceDate = Today, DueDate = null };

        fields.FindIssues(Today).ShouldBeEmpty();
    }

    [Fact]
    public void Amount_MoreThanTwoDecimals_Throws()
    {
        DomainValidationException exception =
            Should.Throw<DomainValidationException>(() => ValidFields with { TaxAmount = 210.005m });

        exception.Code.ShouldBe(DomainErrors.Validation.AmountPrecision);
    }

    [Fact]
    public void ChangesFrom_SameValues_ReturnsNone()
    {
        InvoiceFields copy = ValidFields with { TotalAmount = 1210.0m };

        copy.ChangesFrom(ValidFields).ShouldBeEmpty();
    }

    [Fact]
    public void ChangesFrom_Empty_ListsEverySetFieldFormatted()
    {
        ValidFields.ChangesFrom(InvoiceFields.Empty).ShouldBe(
        [
            new FieldChange(InvoiceField.SupplierName, null, "Acme s.r.o."),
            new FieldChange(InvoiceField.InvoiceNumber, null, "FV-2026-0117"),
            new FieldChange(InvoiceField.InvoiceDate, null, "2026-10-01"),
            new FieldChange(InvoiceField.DueDate, null, "2026-10-31"),
            new FieldChange(InvoiceField.Currency, null, "CZK"),
            new FieldChange(InvoiceField.Subtotal, null, "1000.00"),
            new FieldChange(InvoiceField.TaxAmount, null, "210.00"),
            new FieldChange(InvoiceField.TotalAmount, null, "1210.00"),
            new FieldChange(InvoiceField.PurchaseOrderReference, null, "PO-77"),
        ]);
    }

    [Fact]
    public void ChangesFrom_ChangedAndClearedFields_ListsOnlyThose()
    {
        InvoiceFields edited = ValidFields with { TotalAmount = 1250.5m, PurchaseOrderReference = null };

        edited.ChangesFrom(ValidFields).ShouldBe(
        [
            new FieldChange(InvoiceField.TotalAmount, "1210.00", "1250.50"),
            new FieldChange(InvoiceField.PurchaseOrderReference, "PO-77", null),
        ]);
    }
}
