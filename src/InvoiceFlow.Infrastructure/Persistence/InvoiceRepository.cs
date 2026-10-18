using InvoiceFlow.Application.Abstractions.Persistence;
using InvoiceFlow.Application.Errors;
using InvoiceFlow.Domain.Invoices;
using InvoiceFlow.Infrastructure.Persistence.Tenant;
using Microsoft.EntityFrameworkCore;

namespace InvoiceFlow.Infrastructure.Persistence;

internal sealed class InvoiceRepository(TenantDbContext dbContext) : IInvoiceRepository
{
    public Task<Invoice?> GetAsync(InvoiceId id, CancellationToken cancellationToken) =>
        dbContext.Invoices.SingleOrDefaultAsync(i => i.Id == id, cancellationToken);

    // The loaded version stays the original value of the row version, so the UPDATE also checks it.
    public async Task<Invoice?> GetAsync(InvoiceId id, string expectedVersion, CancellationToken cancellationToken)
    {
        Invoice? invoice = await GetAsync(id, cancellationToken);
        if (invoice is null)
        {
            return null;
        }

        uint current = dbContext.Entry(invoice).Property<uint>(InvoiceConfiguration.VersionProperty).CurrentValue;
        return RowVersion.Format(current) == expectedVersion
            ? invoice
            : throw new ConcurrencyException();
    }

    public async Task<InvoiceId?> FindIdByDocumentSha256Async(string sha256, CancellationToken cancellationToken)
    {
        InvoiceId id = await dbContext.Invoices
            .AsNoTracking()
            .Where(i => i.Document.Sha256 == sha256)
            .OrderBy(i => i.UploadedAt)
            .ThenBy(i => i.Id)
            .Select(i => i.Id)
            .FirstOrDefaultAsync(cancellationToken);

        return id.Value == Guid.Empty ? null : id;
    }

    public void Add(Invoice invoice)
    {
        Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<Invoice> entry = dbContext.Invoices.Add(invoice);
        entry.Property(TenantDbContext.TenantIdProperty).CurrentValue = dbContext.TenantId;
        entry.Reference(i => i.Document).TargetEntry!
            .Property(TenantDbContext.TenantIdProperty).CurrentValue = dbContext.TenantId;
    }
}
