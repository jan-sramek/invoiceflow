using InvoiceFlow.Application.Abstractions.Context;
using InvoiceFlow.Application.Abstractions.Messaging;
using InvoiceFlow.Application.Abstractions.Persistence;
using InvoiceFlow.Application.Errors;
using InvoiceFlow.Domain.Invoices;

namespace InvoiceFlow.Application.Invoices.Submit;

internal sealed class SubmitInvoiceHandler(
    IInvoiceRepository invoices,
    ICurrentUser user,
    TimeProvider time) : ICommandHandler<SubmitInvoiceCommand, InvoiceId>
{
    public async Task<InvoiceId> HandleAsync(SubmitInvoiceCommand command, CancellationToken cancellationToken)
    {
        Invoice invoice = await invoices.GetAsync(command.Id, command.ExpectedVersion, cancellationToken)
            ?? throw new InvoiceNotFoundException();
        invoice.Submit(user.Actor, time.GetUtcNow());

        return invoice.Id;
    }
}
