using InvoiceFlow.Application.Abstractions.Context;
using InvoiceFlow.Application.Abstractions.Persistence;
using InvoiceFlow.Domain.Common;
using InvoiceFlow.Domain.Invoices;

namespace InvoiceFlow.Application.Invoices.Cancel;

internal sealed class CancelInvoiceHandler(IInvoiceRepository invoices, ICurrentUser user, TimeProvider time)
    : InvoiceCommandHandler<CancelInvoiceCommand>(invoices, user, time)
{
    protected override void Apply(Invoice invoice, CancelInvoiceCommand command, Actor actor, DateTimeOffset now) =>
        invoice.Cancel(actor, now, command.Reason!);
}
