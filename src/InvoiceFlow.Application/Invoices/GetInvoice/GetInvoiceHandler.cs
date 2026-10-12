using InvoiceFlow.Application.Abstractions.Messaging;
using InvoiceFlow.Application.Abstractions.Persistence;
using InvoiceFlow.Application.Common;
using InvoiceFlow.Application.Errors;

namespace InvoiceFlow.Application.Invoices.GetInvoice;

internal sealed class GetInvoiceHandler(IInvoiceReadService readService)
    : IQueryHandler<GetInvoiceQuery, Versioned<InvoiceDetailDto>>
{
    public async Task<Versioned<InvoiceDetailDto>> HandleAsync(GetInvoiceQuery query, CancellationToken cancellationToken) =>
        await readService.GetDetailAsync(query.Id, cancellationToken) ?? throw new InvoiceNotFoundException();
}
