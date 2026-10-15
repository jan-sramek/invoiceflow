using InvoiceFlow.Application.Abstractions.Context;
using InvoiceFlow.Application.Abstractions.Persistence;
using InvoiceFlow.Domain.Common;
using InvoiceFlow.Domain.Invoices;

namespace InvoiceFlow.Application.Invoices.MarkPaid;

internal sealed class MarkInvoicePaidHandler(IInvoiceRepository invoices, ICurrentUser user, TimeProvider time)
    : InvoiceCommandHandler<MarkInvoicePaidCommand>(invoices, user, time)
{
    protected override void Apply(Invoice invoice, MarkInvoicePaidCommand command, Actor actor, DateTimeOffset now) =>
        invoice.MarkPaid(actor, now, command.PaidOn!.Value, command.PaymentReference!);
}
