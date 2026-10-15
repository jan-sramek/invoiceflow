namespace InvoiceFlow.Application.Common;

/// <summary>
/// One page of a longer result; <see cref="TotalCount"/> counts every match, not only this page.
/// </summary>
public sealed record PagedList<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);
