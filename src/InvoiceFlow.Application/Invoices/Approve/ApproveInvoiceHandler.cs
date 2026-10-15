using InvoiceFlow.Application.Abstractions.Context;
using InvoiceFlow.Application.Abstractions.Persistence;
using InvoiceFlow.Domain.Common;
using InvoiceFlow.Domain.Invoices;

namespace InvoiceFlow.Application.Invoices.Approve;

internal sealed class ApproveInvoiceHandler(IInvoiceRepository invoices, ICurrentUser user, TimeProvider time)
    : InvoiceCommandHandler<ApproveInvoiceCommand>(invoices, user, time)
{
    protected override void Apply(Invoice invoice, ApproveInvoiceCommand command, Actor actor, DateTimeOffset now) =>
        invoice.Approve(actor, now);
}
