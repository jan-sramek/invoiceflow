using FluentValidation.Results;
using InvoiceFlow.Application.Errors;
using InvoiceFlow.Application.Invoices.UpdateFields;
using InvoiceFlow.Domain.Common;
using InvoiceFlow.Domain.Invoices;

namespace InvoiceFlow.Application.Tests.Invoices;

public class UpdateInvoiceFieldsValidatorTests
{
    private static readonly InvoiceFieldsInput Valid = new(
        " Acme s.r.o. ",
        "FV-2026-0117",
        new DateOnly(2026, 10, 1),
        new DateOnly(2026, 10, 31),
        "czk",
        1000.00m,
        210.00m,
        1210.00m,
        "PO-77");

    [Fact]
    public void ToFields_ValidInput_UsesDomainValueObjects()
    {
        InvoiceFields fields = Valid.ToFields(out IReadOnlyList<RequestFieldError> errors);

        errors.ShouldBeEmpty();
        fields.SupplierName!.Value.ShouldBe("Acme s.r.o.");
        fields.Currency!.Value.ShouldBe("CZK");
        fields.TotalAmount.ShouldBe(1210.00m);
        fields.PurchaseOrderReference!.Value.ShouldBe("PO-77");
    }

    [Fact]
    public void ToFields_BlankOrMissingValues_ClearTheFields()
    {
        InvoiceFields fields = new InvoiceFieldsInput(" ", "", null, null, null, null, null, null, "\t")
            .ToFields(out IReadOnlyList<RequestFieldError> errors);

        errors.ShouldBeEmpty();
        fields.ShouldBe(InvoiceFields.Empty);
    }

    [Fact]
    public void Validate_SeveralInvalidFields_ReportsEveryOneWithItsParams()
    {
        var command = new UpdateInvoiceFieldsCommand(
            new InvoiceId(Guid.CreateVersion7()),
            "1",
            Valid with
            {
                SupplierName = new string('a', SupplierName.MaxLength + 1),
                Currency = "XYZ",
                Subtotal = 1.234m,
                TotalAmount = Money.MaxAbsoluteAmount + 1,
            });

        ValidationResult result = new UpdateInvoiceFieldsValidator().Validate(command);

        result.Errors.Select(e => (e.PropertyName, e.ErrorCode)).ShouldBe(
        [
            ("SupplierName", "validation.max_length"),
            ("Currency", "validation.currency_invalid"),
            ("Subtotal", "validation.amount_precision"),
            ("TotalAmount", "validation.amount_out_of_range"),
        ]);
        result.Errors[0].CustomState.ShouldBe(new Dictionary<string, object?>
        {
            ["field"] = "supplierName",
            ["max"] = SupplierName.MaxLength,
        });
    }
}
