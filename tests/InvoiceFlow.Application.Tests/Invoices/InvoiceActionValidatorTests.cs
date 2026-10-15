using FluentValidation.Results;
using InvoiceFlow.Application.Invoices.Cancel;
using InvoiceFlow.Application.Invoices.MarkPaid;
using InvoiceFlow.Application.Invoices.Reject;
using InvoiceFlow.Domain.Invoices;

namespace InvoiceFlow.Application.Tests.Invoices;

public class InvoiceActionValidatorTests
{
    private static readonly InvoiceId Id = new(Guid.CreateVersion7());

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t ")]
    public void Reject_MissingReason_IsRequired(string? reason)
    {
        ValidationResult result = new RejectInvoiceValidator().Validate(new RejectInvoiceCommand(Id, "1", reason));

        ValidationFailure failure = result.Errors.ShouldHaveSingleItem();
        failure.PropertyName.ShouldBe("Reason");
        failure.ErrorCode.ShouldBe("validation.required");
        failure.CustomState.ShouldBe(new Dictionary<string, object?> { ["field"] = "reason" });
    }

    [Fact]
    public void Reject_ReasonTooLongAfterTrimming_ReportsMaxLength()
    {
        string reason = new('a', Invoice.MaxReasonLength + 1);

        ValidationResult result = new RejectInvoiceValidator().Validate(new RejectInvoiceCommand(Id, "1", reason));

        ValidationFailure failure = result.Errors.ShouldHaveSingleItem();
        failure.ErrorCode.ShouldBe("validation.max_length");
        failure.CustomState.ShouldBe(new Dictionary<string, object?>
        {
            ["field"] = "reason",
            ["max"] = Invoice.MaxReasonLength,
        });
    }

    [Fact]
    public void Reject_ReasonAtMaxLengthWithSurroundingSpaces_IsValid()
    {
        string reason = $"  {new string('a', Invoice.MaxReasonLength)}  ";

        new RejectInvoiceValidator().Validate(new RejectInvoiceCommand(Id, "1", reason)).IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Cancel_MissingReason_IsRequired()
    {
        ValidationResult result = new CancelInvoiceValidator().Validate(new CancelInvoiceCommand(Id, "1", " "));

        result.Errors.ShouldHaveSingleItem().ErrorCode.ShouldBe("validation.required");
    }

    [Fact]
    public void MarkPaid_MissingValues_ReportsEveryField()
    {
        ValidationResult result = new MarkInvoicePaidValidator().Validate(new MarkInvoicePaidCommand(Id, "1", null, ""));

        result.Errors.Select(e => (e.PropertyName, e.ErrorCode)).ShouldBe(
        [
            ("PaidOn", "validation.required"),
            ("PaymentReference", "validation.required"),
        ]);
        result.Errors[0].CustomState.ShouldBe(new Dictionary<string, object?> { ["field"] = "paidOn" });
    }

    [Fact]
    public void MarkPaid_PaymentReferenceTooLong_ReportsMaxLength()
    {
        var command = new MarkInvoicePaidCommand(
            Id, "1", new DateOnly(2026, 10, 12), new string('a', Invoice.MaxPaymentReferenceLength + 1));

        ValidationResult result = new MarkInvoicePaidValidator().Validate(command);

        result.Errors.ShouldHaveSingleItem().ErrorCode.ShouldBe("validation.max_length");
    }
}
