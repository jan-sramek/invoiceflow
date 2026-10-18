using FluentValidation.Results;
using InvoiceFlow.Application.Invoices.Upload;

namespace InvoiceFlow.Application.Tests.Invoices;

public class UploadInvoiceValidatorTests
{
    [Fact]
    public void Validate_MissingFile_IsRequired()
    {
        ValidationResult result = new UploadInvoiceValidator().Validate(new UploadInvoiceCommand(null, "a.pdf"));

        ValidationFailure failure = result.Errors.ShouldHaveSingleItem();
        failure.PropertyName.ShouldBe("File");
        failure.ErrorCode.ShouldBe("validation.required");
        failure.CustomState.ShouldBe(new Dictionary<string, object?> { ["field"] = "file" });
    }

    [Fact]
    public void Validate_FilePresent_Passes()
    {
        using var file = new MemoryStream("%PDF-"u8.ToArray());
        new UploadInvoiceValidator().Validate(new UploadInvoiceCommand(file, "a.pdf")).IsValid.ShouldBeTrue();
    }
}
