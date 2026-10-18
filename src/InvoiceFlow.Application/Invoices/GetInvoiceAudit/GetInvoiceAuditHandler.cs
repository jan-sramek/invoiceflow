using InvoiceFlow.Application.Abstractions.Messaging;
using InvoiceFlow.Application.Abstractions.Persistence;
using InvoiceFlow.Application.Common;
using InvoiceFlow.Application.Errors;

namespace InvoiceFlow.Application.Invoices.GetInvoiceAudit;

internal sealed class GetInvoiceAuditHandler(IInvoiceReadService readService)
    : IQueryHandler<GetInvoiceAuditQuery, PagedList<InvoiceAuditEntryDto>>
{
    public async Task<PagedList<InvoiceAuditEntryDto>> HandleAsync(
        GetInvoiceAuditQuery query, CancellationToken cancellationToken)
    {
        PagedList<InvoiceAuditEntryDto>? page = await readService.GetAuditAsync(
            query.Id, query.Page, query.PageSize, cancellationToken);

        return page ?? throw new InvoiceNotFoundException();
    }
}
