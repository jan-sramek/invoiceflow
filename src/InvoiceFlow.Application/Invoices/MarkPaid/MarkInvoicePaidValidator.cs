using FluentValidation;
using InvoiceFlow.Application.Common;
using InvoiceFlow.Domain.Invoices;

namespace InvoiceFlow.Application.Invoices.MarkPaid;

internal sealed class MarkInvoicePaidValidator : AbstractValidator<MarkInvoicePaidCommand>
{
    public MarkInvoicePaidValidator()
    {
        RuleFor(command => command.PaidOn).Required("paidOn");
        RuleFor(command => command.PaymentReference)
            .RequiredText("paymentReference", Invoice.MaxPaymentReferenceLength);
    }
}
