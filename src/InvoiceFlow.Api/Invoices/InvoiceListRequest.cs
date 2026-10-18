using InvoiceFlow.Application.Invoices.ListInvoices;
using InvoiceFlow.Domain.Invoices;

namespace InvoiceFlow.Api.Invoices;

/// <summary>
/// Query parameters of the invoice list; all are optional and ranges include both ends.
/// </summary>
public sealed record InvoiceListRequest
{
    /// <summary>
    /// Repeat to match any of several statuses. Cancelled invoices are listed only when asked for here.
    /// </summary>
    public InvoiceStatus[]? Status { get; init; }

    public DateOnly? InvoiceDateFrom { get; init; }

    public DateOnly? InvoiceDateTo { get; init; }

    public DateOnly? DueDateFrom { get; init; }

    public DateOnly? DueDateTo { get; init; }

    public decimal? TotalAmountFrom { get; init; }

    public decimal? TotalAmountTo { get; init; }

    public string? Currency { get; init; }

    /// <summary>
    /// Only approved invoices whose due date has passed.
    /// </summary>
    public bool? Overdue { get; init; }

    /// <summary>
    /// Only invoices with this duplicate status (<c>None</c>, <c>Suspected</c> or <c>Cleared</c>).
    /// </summary>
    public DuplicateStatus? DuplicateStatus { get; init; }

    /// <summary>
    /// Part of the supplier name or invoice number, in any letter case.
    /// </summary>
    public string? Search { get; init; }

    /// <summary>
    /// <c>uploadedAt</c>, <c>invoiceDate</c>, <c>dueDate</c>, <c>totalAmount</c>, <c>status</c> or
    /// <c>supplierName</c>; a leading <c>-</c> sorts descending. Defaults to <c>-uploadedAt</c>.
    /// </summary>
    public string? Sort { get; init; }

    public int? Page { get; init; }

    /// <summary>
    /// 25 unless given; at most 100.
    /// </summary>
    public int? PageSize { get; init; }

    public ListInvoicesQuery ToQuery() => new()
    {
        Status = Status ?? [],
        InvoiceDateFrom = InvoiceDateFrom,
        InvoiceDateTo = InvoiceDateTo,
        DueDateFrom = DueDateFrom,
        DueDateTo = DueDateTo,
        TotalAmountFrom = TotalAmountFrom,
        TotalAmountTo = TotalAmountTo,
        Currency = Currency,
        Overdue = Overdue ?? false,
        DuplicateStatus = DuplicateStatus,
        Search = Search,
        Sort = Sort,
        Page = Page ?? 1,
        PageSize = PageSize ?? ListInvoicesQuery.DefaultPageSize,
    };
}
