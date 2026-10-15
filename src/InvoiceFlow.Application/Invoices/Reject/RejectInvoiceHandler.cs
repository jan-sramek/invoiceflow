using InvoiceFlow.Application.Abstractions.Context;
using InvoiceFlow.Application.Abstractions.Persistence;
using InvoiceFlow.Domain.Common;
using InvoiceFlow.Domain.Invoices;

namespace InvoiceFlow.Application.Invoices.Reject;

internal sealed class RejectInvoiceHandler(IInvoiceRepository invoices, ICurrentUser user, TimeProvider time)
    : InvoiceCommandHandler<RejectInvoiceCommand>(invoices, user, time)
{
    protected override void Apply(Invoice invoice, RejectInvoiceCommand command, Actor actor, DateTimeOffset now) =>
        invoice.Reject(actor, now, command.Reason!);
}
