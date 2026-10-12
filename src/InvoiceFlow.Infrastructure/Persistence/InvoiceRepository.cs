using InvoiceFlow.Application.Abstractions.Persistence;
using InvoiceFlow.Domain.Invoices;
using InvoiceFlow.Infrastructure.Persistence.Tenant;
using Microsoft.EntityFrameworkCore;

namespace InvoiceFlow.Infrastructure.Persistence;

internal sealed class InvoiceRepository(TenantDbContext dbContext) : IInvoiceRepository
{
    public Task<Invoice?> GetAsync(InvoiceId id, CancellationToken cancellationToken) =>
        dbContext.Invoices.SingleOrDefaultAsync(i => i.Id == id, cancellationToken);

    public void Add(Invoice invoice) =>
        dbContext.Invoices.Add(invoice).Property(TenantDbContext.TenantIdProperty).CurrentValue = dbContext.TenantId;
}
