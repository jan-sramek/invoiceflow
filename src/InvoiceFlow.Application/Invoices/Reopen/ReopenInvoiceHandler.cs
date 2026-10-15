using InvoiceFlow.Application.Abstractions.Context;
using InvoiceFlow.Application.Abstractions.Persistence;
using InvoiceFlow.Domain.Common;
using InvoiceFlow.Domain.Invoices;

namespace InvoiceFlow.Application.Invoices.Reopen;

internal sealed class ReopenInvoiceHandler(IInvoiceRepository invoices, ICurrentUser user, TimeProvider time)
    : InvoiceCommandHandler<ReopenInvoiceCommand>(invoices, user, time)
{
    protected override void Apply(Invoice invoice, ReopenInvoiceCommand command, Actor actor, DateTimeOffset now) =>
        invoice.Reopen(actor, now);
}
