using FluentValidation;
using InvoiceFlow.Application.Common;

namespace InvoiceFlow.Application.Invoices.Upload;

internal sealed class UploadInvoiceValidator : AbstractValidator<UploadInvoiceCommand>
{
    public UploadInvoiceValidator() => RuleFor(command => command.File).RequiredObject("file");
}
