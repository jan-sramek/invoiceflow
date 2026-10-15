using InvoiceFlow.Application.Abstractions.Context;
using InvoiceFlow.Application.Abstractions.Persistence;
using InvoiceFlow.Domain.Common;
using InvoiceFlow.Domain.Invoices;

namespace InvoiceFlow.Application.Invoices.EnterManually;

internal sealed class EnterInvoiceManuallyHandler(IInvoiceRepository invoices, ICurrentUser user, TimeProvider time)
    : InvoiceCommandHandler<EnterInvoiceManuallyCommand>(invoices, user, time)
{
    protected override void Apply(
        Invoice invoice, EnterInvoiceManuallyCommand command, Actor actor, DateTimeOffset now) =>
        invoice.EnterManually(actor, now);
}
