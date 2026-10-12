using InvoiceFlow.Application.Abstractions.Persistence;
using InvoiceFlow.Application.Common;
using InvoiceFlow.Application.Invoices.GetInvoice;
using InvoiceFlow.Domain.Invoices;
using InvoiceFlow.Infrastructure.Persistence.Tenant;
using Microsoft.EntityFrameworkCore;

namespace InvoiceFlow.Infrastructure.Persistence;

internal sealed class InvoiceReadService(TenantDbContext dbContext) : IInvoiceReadService
{
    public async Task<Versioned<InvoiceDetailDto>?> GetDetailAsync(InvoiceId id, CancellationToken cancellationToken)
    {
        var row = await dbContext.Invoices
            .AsNoTracking()
            .Where(i => i.Id == id)
            .Select(i => new
            {
                Detail = new InvoiceDetailDto(
                    i.Id.Value,
                    i.Status,
                    new InvoiceFieldsDto(
                        i.Fields.SupplierName == null ? null : i.Fields.SupplierName.Value,
                        i.Fields.InvoiceNumber == null ? null : i.Fields.InvoiceNumber.Value,
                        i.Fields.InvoiceDate,
                        i.Fields.DueDate,
                        i.Fields.Currency == null ? null : i.Fields.Currency.Value,
                        i.Fields.Subtotal,
                        i.Fields.TaxAmount,
                        i.Fields.TotalAmount,
                        i.Fields.PurchaseOrderReference == null ? null : i.Fields.PurchaseOrderReference.Value),
                    new ActorDto(i.UploadedBy.Type, i.UploadedBy.Id, i.UploadedBy.Name),
                    i.UploadedAt,
                    i.SubmittedBy == null ? null : new ActorDto(i.SubmittedBy.Type, i.SubmittedBy.Id, i.SubmittedBy.Name),
                    i.PaidOn,
                    i.PaymentReference),
                Version = EF.Property<uint>(i, InvoiceConfiguration.VersionProperty),
            })
            .SingleOrDefaultAsync(cancellationToken);

        return row is null
            ? null
            : new Versioned<InvoiceDetailDto>(row.Detail, RowVersion.Format(row.Version));
    }
}
