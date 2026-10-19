using InvoiceFlow.Domain.Invoices;
using InvoiceFlow.Domain.Tenants;

namespace InvoiceFlow.Application.Abstractions.Messaging;

/// <summary>
/// Publishes invoice lifecycle events after a successful unit-of-work commit.
/// </summary>
public interface IInvoiceEventPublisher
{
    Task PublishUploadedAsync(TenantId tenantId, InvoiceId invoiceId, CancellationToken cancellationToken);
}
