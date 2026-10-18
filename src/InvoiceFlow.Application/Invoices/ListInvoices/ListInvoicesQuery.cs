using InvoiceFlow.Application.Abstractions.Messaging;
using InvoiceFlow.Application.Common;
using InvoiceFlow.Domain.Invoices;

namespace InvoiceFlow.Application.Invoices.ListInvoices;

/// <summary>
/// Property names match the query string, so errors point at the parameter the client sent.
/// Cancelled invoices are left out unless <see cref="Status"/> asks for them.
/// </summary>
public sealed record ListInvoicesQuery : IQuery<PagedList<InvoiceSummaryDto>>
{
    public const int DefaultPageSize = 25;
    public const int MaxPageSize = 100;
    public const int MaxPage = 10_000;
    public const int MaxSearchLength = 200;

    public IReadOnlyList<InvoiceStatus> Status { get; init; } = [];

    public DateOnly? InvoiceDateFrom { get; init; }

    public DateOnly? InvoiceDateTo { get; init; }

    public DateOnly? DueDateFrom { get; init; }

    public DateOnly? DueDateTo { get; init; }

    public decimal? TotalAmountFrom { get; init; }

    public decimal? TotalAmountTo { get; init; }

    public string? Currency { get; init; }

    public bool Overdue { get; init; }

    /// <summary>
    /// When set, only invoices with this duplicate status.
    /// </summary>
    public DuplicateStatus? DuplicateStatus { get; init; }

    /// <summary>
    /// Part of the supplier name or invoice number, in any letter case.
    /// </summary>
    public string? Search { get; init; }

    public string? Sort { get; init; }

    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = DefaultPageSize;
}
