using InvoiceFlow.Application.Abstractions.Context;
using InvoiceFlow.Application.Abstractions.Persistence;
using InvoiceFlow.Domain.Common;
using InvoiceFlow.Domain.Invoices;

namespace InvoiceFlow.Application.Invoices.AcknowledgeDuplicate;

internal sealed class AcknowledgeNotDuplicateHandler(IInvoiceRepository invoices, ICurrentUser user, TimeProvider time)
    : InvoiceCommandHandler<AcknowledgeNotDuplicateCommand>(invoices, user, time)
{
    protected override void Apply(
        Invoice invoice, AcknowledgeNotDuplicateCommand command, Actor actor, DateTimeOffset now) =>
        invoice.AcknowledgeNotDuplicate(actor, now, command.Reason!);
}
