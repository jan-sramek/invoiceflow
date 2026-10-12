using InvoiceFlow.Application.Abstractions.Context;
using InvoiceFlow.Application.Abstractions.Messaging;
using InvoiceFlow.Application.Abstractions.Persistence;
using InvoiceFlow.Application.Errors;
using InvoiceFlow.Domain.Invoices;

namespace InvoiceFlow.Application.Invoices.EnterManually;

internal sealed class EnterInvoiceManuallyHandler(
    IInvoiceRepository invoices,
    ICurrentUser user,
    TimeProvider time) : ICommandHandler<EnterInvoiceManuallyCommand, InvoiceId>
{
    public async Task<InvoiceId> HandleAsync(EnterInvoiceManuallyCommand command, CancellationToken cancellationToken)
    {
        Invoice invoice = await invoices.GetAsync(command.Id, command.ExpectedVersion, cancellationToken)
            ?? throw new InvoiceNotFoundException();
        invoice.EnterManually(user.Actor, time.GetUtcNow());

        return invoice.Id;
    }
}
