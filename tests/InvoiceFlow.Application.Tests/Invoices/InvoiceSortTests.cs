using InvoiceFlow.Application.Invoices.ListInvoices;

namespace InvoiceFlow.Application.Tests.Invoices;

public class InvoiceSortTests
{
    [Theory]
    [InlineData(null, InvoiceSortField.UploadedAt, true)]
    [InlineData("", InvoiceSortField.UploadedAt, true)]
    [InlineData("dueDate", InvoiceSortField.DueDate, false)]
    [InlineData("-totalAmount", InvoiceSortField.TotalAmount, true)]
    [InlineData("supplierName", InvoiceSortField.SupplierName, false)]
    public void TryParse_KnownValue_ReturnsFieldAndDirection(string? value, InvoiceSortField field, bool descending)
    {
        InvoiceSort.TryParse(value, out InvoiceSort? sort).ShouldBeTrue();

        sort.ShouldBe(new InvoiceSort(field, descending));
    }

    [Theory]
    [InlineData("amount")]
    [InlineData("DueDate")]
    [InlineData("--dueDate")]
    [InlineData("+dueDate")]
    [InlineData("2")]
    [InlineData("-")]
    public void TryParse_UnknownValue_Fails(string value)
    {
        InvoiceSort.TryParse(value, out InvoiceSort? sort).ShouldBeFalse();
        sort.ShouldBeNull();
    }

    [Fact]
    public void Names_AreTheFieldsInCamelCase()
    {
        InvoiceSort.Names.ShouldBe(["uploadedAt", "invoiceDate", "dueDate", "totalAmount", "status", "supplierName"]);
    }
}
