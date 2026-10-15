using FluentValidation;
using InvoiceFlow.Application.Common;
using InvoiceFlow.Domain.Invoices;

namespace InvoiceFlow.Application.Invoices.Cancel;

internal sealed class CancelInvoiceValidator : AbstractValidator<CancelInvoiceCommand>
{
    public CancelInvoiceValidator() =>
        RuleFor(command => command.Reason).RequiredText("reason", Invoice.MaxReasonLength);
}
