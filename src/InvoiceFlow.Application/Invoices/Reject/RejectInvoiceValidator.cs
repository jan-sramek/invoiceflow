using FluentValidation;
using InvoiceFlow.Application.Common;
using InvoiceFlow.Domain.Invoices;

namespace InvoiceFlow.Application.Invoices.Reject;

internal sealed class RejectInvoiceValidator : AbstractValidator<RejectInvoiceCommand>
{
    public RejectInvoiceValidator() =>
        RuleFor(command => command.Reason).RequiredText("reason", Invoice.MaxReasonLength);
}
