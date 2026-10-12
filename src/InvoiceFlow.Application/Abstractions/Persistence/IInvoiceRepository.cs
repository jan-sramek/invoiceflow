using InvoiceFlow.Domain.Invoices;

namespace InvoiceFlow.Application.Abstractions.Persistence;

public interface IInvoiceRepository
{
    Task<Invoice?> GetAsync(InvoiceId id, CancellationToken cancellationToken);

    void Add(Invoice invoice);
}
