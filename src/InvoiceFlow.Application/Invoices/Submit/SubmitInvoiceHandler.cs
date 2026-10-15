using InvoiceFlow.Application.Abstractions.Context;
using InvoiceFlow.Application.Abstractions.Persistence;
using InvoiceFlow.Domain.Common;
using InvoiceFlow.Domain.Invoices;

namespace InvoiceFlow.Application.Invoices.Submit;

internal sealed class SubmitInvoiceHandler(IInvoiceRepository invoices, ICurrentUser user, TimeProvider time)
    : InvoiceCommandHandler<SubmitInvoiceCommand>(invoices, user, time)
{
    protected override void Apply(Invoice invoice, SubmitInvoiceCommand command, Actor actor, DateTimeOffset now) =>
        invoice.Submit(actor, now);
}
