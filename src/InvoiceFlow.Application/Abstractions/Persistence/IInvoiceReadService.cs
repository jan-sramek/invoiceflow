using InvoiceFlow.Application.Common;
using InvoiceFlow.Application.Invoices.GetInvoice;
using InvoiceFlow.Application.Invoices.ListInvoices;
using InvoiceFlow.Domain.Invoices;

namespace InvoiceFlow.Application.Abstractions.Persistence;

/// <summary>
/// Reads invoices of the current tenant straight into response shapes, without loading aggregates.
/// </summary>
public interface IInvoiceReadService
{
    Task<Versioned<InvoiceDetailDto>?> GetDetailAsync(InvoiceId id, CancellationToken cancellationToken);

    /// <param name="query">Already validated.</param>
    /// <param name="sort">The parsed <see cref="ListInvoicesQuery.Sort"/>.</param>
    /// <param name="today">The date that decides whether an invoice is overdue.</param>
    /// <param name="cancellationToken">Cancels the database queries.</param>
    Task<PagedList<InvoiceSummaryDto>> ListAsync(
        ListInvoicesQuery query, InvoiceSort sort, DateOnly today, CancellationToken cancellationToken);
}
