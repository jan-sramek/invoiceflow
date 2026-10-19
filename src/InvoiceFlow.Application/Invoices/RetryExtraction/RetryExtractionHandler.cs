using InvoiceFlow.Application.Abstractions.Context;
using InvoiceFlow.Application.Abstractions.Messaging;
using InvoiceFlow.Application.Abstractions.Persistence;
using InvoiceFlow.Domain.Common;
using InvoiceFlow.Domain.Invoices;
using InvoiceFlow.Domain.Tenants;

namespace InvoiceFlow.Application.Invoices.RetryExtraction;

internal sealed class RetryExtractionHandler(
    IInvoiceRepository invoices,
    ICurrentUser user,
    ITenantContext tenant,
    IUnitOfWork unitOfWork,
    IInvoiceEventPublisher events,
    TimeProvider time) : InvoiceCommandHandler<RetryExtractionCommand>(invoices, user, time)
{
    protected override void Apply(
        Invoice invoice, RetryExtractionCommand command, Actor actor, DateTimeOffset now)
    {
        invoice.RetryExtraction(actor, now);

        TenantId tenantId = tenant.TenantId;
        InvoiceId invoiceId = invoice.Id;
        unitOfWork.AfterCommit(ct => events.PublishUploadedAsync(tenantId, invoiceId, ct));
    }
}
