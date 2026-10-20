using InvoiceFlow.Application.Abstractions.Context;
using InvoiceFlow.Application.Abstractions.Messaging;
using InvoiceFlow.Application.Abstractions.Persistence;
using InvoiceFlow.Application.Errors;
using InvoiceFlow.Domain.Invoices;

namespace InvoiceFlow.Application.Invoices.RetryExtraction;

internal sealed class RetryExtractionHandler(
    IInvoiceRepository invoices,
    ICurrentUser user,
    ITenantContext tenant,
    IInvoiceEventPublisher events,
    TimeProvider time) : ICommandHandler<RetryExtractionCommand, InvoiceId>
{
    public async Task<InvoiceId> HandleAsync(RetryExtractionCommand command, CancellationToken cancellationToken)
    {
        Invoice invoice = await invoices.GetAsync(command.Id, command.ExpectedVersion, cancellationToken)
            ?? throw new InvoiceNotFoundException();

        invoice.RetryExtraction(user.Actor, time.GetUtcNow());
        await events.PublishUploadedAsync(tenant.TenantId, invoice.Id, cancellationToken);
        return invoice.Id;
    }
}
