using InvoiceFlow.Application.Abstractions.Messaging;
using InvoiceFlow.Domain.Invoices;
using InvoiceFlow.Domain.Tenants;

namespace InvoiceFlow.Infrastructure.Messaging;

internal sealed class NoOpInvoiceEventPublisher : IInvoiceEventPublisher
{
    public Task PublishUploadedAsync(TenantId tenantId, InvoiceId invoiceId, CancellationToken cancellationToken) =>
        Task.CompletedTask;
}
