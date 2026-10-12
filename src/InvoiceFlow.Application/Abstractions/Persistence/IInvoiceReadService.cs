using InvoiceFlow.Application.Common;
using InvoiceFlow.Application.Invoices.GetInvoice;
using InvoiceFlow.Domain.Invoices;

namespace InvoiceFlow.Application.Abstractions.Persistence;

/// <summary>
/// Reads invoices of the current tenant straight into response shapes, without loading aggregates.
/// </summary>
public interface IInvoiceReadService
{
    Task<Versioned<InvoiceDetailDto>?> GetDetailAsync(InvoiceId id, CancellationToken cancellationToken);
}
