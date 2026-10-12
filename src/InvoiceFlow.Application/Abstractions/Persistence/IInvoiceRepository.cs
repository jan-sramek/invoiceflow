using InvoiceFlow.Domain.Invoices;

namespace InvoiceFlow.Application.Abstractions.Persistence;

public interface IInvoiceRepository
{
    Task<Invoice?> GetAsync(InvoiceId id, CancellationToken cancellationToken);

    /// <summary>
    /// Loads an invoice to change it, provided it is still at the version the caller saw. Saving fails the
    /// same way if someone else changes it in the meantime.
    /// </summary>
    /// <exception cref="Errors.ConcurrencyException">The invoice is at a different version.</exception>
    Task<Invoice?> GetAsync(InvoiceId id, string expectedVersion, CancellationToken cancellationToken);

    void Add(Invoice invoice);
}
