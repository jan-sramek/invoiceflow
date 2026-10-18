using System.Linq.Expressions;
using InvoiceFlow.Application.Abstractions.Persistence;
using InvoiceFlow.Application.Common;
using InvoiceFlow.Application.Invoices.GetInvoice;
using InvoiceFlow.Application.Invoices.ListInvoices;
using InvoiceFlow.Domain.Common;
using InvoiceFlow.Domain.Invoices;
using InvoiceFlow.Infrastructure.Persistence.Tenant;
using Microsoft.EntityFrameworkCore;

namespace InvoiceFlow.Infrastructure.Persistence;

internal sealed class InvoiceReadService(TenantDbContext dbContext) : IInvoiceReadService
{
    // Lifecycle order rather than alphabetical: CASE WHEN status = 'Uploaded' THEN 0 ...
    private static readonly Expression<Func<Invoice, int>> StatusOrder = BuildStatusOrder();

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
                    new DocumentDto(i.Document.FileName, i.Document.SizeBytes),
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

    public async Task<PagedList<InvoiceSummaryDto>> ListAsync(
        ListInvoicesQuery query, InvoiceSort sort, DateOnly today, CancellationToken cancellationToken)
    {
        IQueryable<Invoice> invoices = Filter(dbContext.Invoices.AsNoTracking(), query, today);

        int totalCount = await invoices.CountAsync(cancellationToken);
        List<InvoiceSummaryDto> items = await Order(invoices, sort)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(i => new InvoiceSummaryDto(
                i.Id.Value,
                i.Status,
                i.Fields.SupplierName == null ? null : i.Fields.SupplierName.Value,
                i.Fields.InvoiceNumber == null ? null : i.Fields.InvoiceNumber.Value,
                i.Fields.InvoiceDate,
                i.Fields.DueDate,
                i.Fields.Currency == null ? null : i.Fields.Currency.Value,
                i.Fields.TotalAmount,
                i.UploadedAt,
                i.Status == InvoiceStatus.Approved && i.Fields.DueDate < today))
            .ToListAsync(cancellationToken);

        return new PagedList<InvoiceSummaryDto>(items, query.Page, query.PageSize, totalCount);
    }

    private static IQueryable<Invoice> Filter(IQueryable<Invoice> invoices, ListInvoicesQuery query, DateOnly today)
    {
        invoices = query.Status.Count > 0
            ? invoices.Where(i => query.Status.Contains(i.Status))
            : invoices.Where(i => i.Status != InvoiceStatus.Cancelled);

        if (query.InvoiceDateFrom is { } invoiceDateFrom)
        {
            invoices = invoices.Where(i => i.Fields.InvoiceDate >= invoiceDateFrom);
        }

        if (query.InvoiceDateTo is { } invoiceDateTo)
        {
            invoices = invoices.Where(i => i.Fields.InvoiceDate <= invoiceDateTo);
        }

        if (query.DueDateFrom is { } dueDateFrom)
        {
            invoices = invoices.Where(i => i.Fields.DueDate >= dueDateFrom);
        }

        if (query.DueDateTo is { } dueDateTo)
        {
            invoices = invoices.Where(i => i.Fields.DueDate <= dueDateTo);
        }

        if (query.TotalAmountFrom is { } totalFrom)
        {
            invoices = invoices.Where(i => i.Fields.TotalAmount >= totalFrom);
        }

        if (query.TotalAmountTo is { } totalTo)
        {
            invoices = invoices.Where(i => i.Fields.TotalAmount <= totalTo);
        }

        if (query.Currency is not null)
        {
            var currency = CurrencyCode.Create(query.Currency);
            invoices = invoices.Where(i => i.Fields.Currency == currency);
        }

        if (query.Overdue)
        {
            invoices = invoices.Where(i => i.Status == InvoiceStatus.Approved && i.Fields.DueDate < today);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            string pattern = $"%{EscapeLike(query.Search.Trim())}%";
            invoices = invoices.Where(i =>
                EF.Functions.ILike((string)(object)i.Fields.SupplierName!, pattern, @"\")
                || EF.Functions.ILike((string)(object)i.Fields.InvoiceNumber!, pattern, @"\"));
        }

        return invoices;
    }

    private static IOrderedQueryable<Invoice> Order(IQueryable<Invoice> invoices, InvoiceSort sort)
    {
        IOrderedQueryable<Invoice> ordered = sort.Field switch
        {
            InvoiceSortField.UploadedAt => By(invoices, i => i.UploadedAt, sort.Descending),
            InvoiceSortField.InvoiceDate => NullsLast(invoices, i => i.Fields.InvoiceDate, sort.Descending),
            InvoiceSortField.DueDate => NullsLast(invoices, i => i.Fields.DueDate, sort.Descending),
            InvoiceSortField.TotalAmount => NullsLast(invoices, i => i.Fields.TotalAmount, sort.Descending),
            InvoiceSortField.Status => By(invoices, StatusOrder, sort.Descending),
            InvoiceSortField.SupplierName => NullsLast(invoices, i => i.Fields.SupplierName, sort.Descending),
            _ => throw new ArgumentOutOfRangeException(nameof(sort), sort.Field, null),
        };

        // Equal keys would otherwise come back in any order and rows could repeat or vanish between pages.
        return sort.Descending ? ordered.ThenByDescending(i => i.Id) : ordered.ThenBy(i => i.Id);
    }

    private static IOrderedQueryable<Invoice> By<TKey>(
        IQueryable<Invoice> invoices, Expression<Func<Invoice, TKey>> key, bool descending) =>
        descending ? invoices.OrderByDescending(key) : invoices.OrderBy(key);

    // PostgreSQL puts NULLs first when descending; missing values belong at the end either way.
    private static IOrderedQueryable<Invoice> NullsLast<TKey>(
        IQueryable<Invoice> invoices, Expression<Func<Invoice, TKey>> key, bool descending)
    {
        var isMissing = Expression.Lambda<Func<Invoice, bool>>(
            Expression.Equal(key.Body, Expression.Constant(null, key.Body.Type)), key.Parameters);
        IOrderedQueryable<Invoice> missingLast = invoices.OrderBy(isMissing);

        return descending ? missingLast.ThenByDescending(key) : missingLast.ThenBy(key);
    }

    private static Expression<Func<Invoice, int>> BuildStatusOrder()
    {
        ParameterExpression invoice = Expression.Parameter(typeof(Invoice), "i");
        MemberExpression status = Expression.Property(invoice, nameof(Invoice.Status));
        Expression order = Expression.Constant(int.MaxValue);

        foreach (InvoiceStatus value in Enum.GetValues<InvoiceStatus>().Reverse())
        {
            order = Expression.Condition(
                Expression.Equal(status, Expression.Constant(value)), Expression.Constant((int)value), order);
        }

        return Expression.Lambda<Func<Invoice, int>>(order, invoice);
    }

    private static string EscapeLike(string value) =>
        value.Replace(@"\", @"\\", StringComparison.Ordinal)
            .Replace("%", @"\%", StringComparison.Ordinal)
            .Replace("_", @"\_", StringComparison.Ordinal);
}
