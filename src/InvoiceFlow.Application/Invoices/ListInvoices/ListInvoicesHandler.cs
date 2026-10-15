using InvoiceFlow.Application.Abstractions.Messaging;
using InvoiceFlow.Application.Abstractions.Persistence;
using InvoiceFlow.Application.Common;

namespace InvoiceFlow.Application.Invoices.ListInvoices;

internal sealed class ListInvoicesHandler(IInvoiceReadService readService, TimeProvider time)
    : IQueryHandler<ListInvoicesQuery, PagedList<InvoiceSummaryDto>>
{
    public Task<PagedList<InvoiceSummaryDto>> HandleAsync(ListInvoicesQuery query, CancellationToken cancellationToken)
    {
        if (!InvoiceSort.TryParse(query.Sort, out InvoiceSort? sort))
        {
            throw new ArgumentException($"Unknown sort '{query.Sort}'; the validator should have rejected it.", nameof(query));
        }

        // Without a tenant time zone, an invoice becomes overdue when its due date has passed in UTC.
        var today = DateOnly.FromDateTime(time.GetUtcNow().UtcDateTime);

        return readService.ListAsync(query, sort, today, cancellationToken);
    }
}
