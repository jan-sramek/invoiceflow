using InvoiceFlow.Domain.Errors;
using InvoiceFlow.Domain.Invoices;

namespace InvoiceFlow.Domain.Tests.Invoices;

public class TextValueTests
{
    [Theory]
    [InlineData("supplierName")]
    [InlineData("invoiceNumber")]
    [InlineData("purchaseOrderReference")]
    public void Create_TrimsValueUpToMaxLength(string field)
    {
        Create(field, "  ACME-1 ").ShouldBe("ACME-1");
        Create(field, new string('x', MaxLength(field))).Length.ShouldBe(MaxLength(field));
    }

    [Theory]
    [InlineData("supplierName", null)]
    [InlineData("supplierName", "   ")]
    [InlineData("invoiceNumber", "")]
    [InlineData("purchaseOrderReference", " ")]
    public void Create_Blank_Throws(string field, string? value)
    {
        DomainValidationException exception = Should.Throw<DomainValidationException>(() => Create(field, value));

        exception.Code.ShouldBe(DomainErrors.Validation.Required);
        exception.Params["field"].ShouldBe(field);
    }

    [Theory]
    [InlineData("supplierName")]
    [InlineData("invoiceNumber")]
    [InlineData("purchaseOrderReference")]
    public void Create_TooLong_Throws(string field)
    {
        DomainValidationException exception =
            Should.Throw<DomainValidationException>(() => Create(field, new string('x', MaxLength(field) + 1)));

        exception.Code.ShouldBe(DomainErrors.Validation.MaxLength);
        exception.Params["field"].ShouldBe(field);
        exception.Params["max"].ShouldBe(MaxLength(field));
    }

    private static string Create(string field, string? value) => field switch
    {
        "supplierName" => SupplierName.Create(value).Value,
        "invoiceNumber" => InvoiceNumber.Create(value).Value,
        "purchaseOrderReference" => PurchaseOrderReference.Create(value).Value,
        _ => throw new ArgumentOutOfRangeException(nameof(field), field, null),
    };

    private static int MaxLength(string field) => field switch
    {
        "supplierName" => SupplierName.MaxLength,
        "invoiceNumber" => InvoiceNumber.MaxLength,
        "purchaseOrderReference" => PurchaseOrderReference.MaxLength,
        _ => throw new ArgumentOutOfRangeException(nameof(field), field, null),
    };
}
