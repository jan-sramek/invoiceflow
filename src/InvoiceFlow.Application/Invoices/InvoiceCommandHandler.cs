using InvoiceFlow.Application.Abstractions.Context;
using InvoiceFlow.Application.Abstractions.Messaging;
using InvoiceFlow.Application.Abstractions.Persistence;
using InvoiceFlow.Application.Errors;
using InvoiceFlow.Domain.Common;
using InvoiceFlow.Domain.Invoices;

namespace InvoiceFlow.Application.Invoices;

/// <summary>
/// Loads the invoice at the expected version and lets the use case apply its change as the current user.
/// </summary>
internal abstract class InvoiceCommandHandler<TCommand>(
    IInvoiceRepository invoices,
    ICurrentUser user,
    TimeProvider time) : ICommandHandler<TCommand, InvoiceId>
    where TCommand : IInvoiceCommand
{
    public async Task<InvoiceId> HandleAsync(TCommand command, CancellationToken cancellationToken)
    {
        Invoice invoice = await invoices.GetAsync(command.Id, command.ExpectedVersion, cancellationToken)
            ?? throw new InvoiceNotFoundException();
        Apply(invoice, command, user.Actor, time.GetUtcNow());

        return invoice.Id;
    }

    protected abstract void Apply(Invoice invoice, TCommand command, Actor actor, DateTimeOffset now);
}
