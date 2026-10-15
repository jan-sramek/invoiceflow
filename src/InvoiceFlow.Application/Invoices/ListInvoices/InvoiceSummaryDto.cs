using InvoiceFlow.Domain.Invoices;

namespace InvoiceFlow.Application.Invoices.ListInvoices;

/// <summary>
/// One row of the invoice list. An invoice is overdue when it is approved, not paid, and its due date has passed.
/// </summary>
public sealed record InvoiceSummaryDto(
    Guid Id,
    InvoiceStatus Status,
    string? SupplierName,
    string? InvoiceNumber,
    DateOnly? InvoiceDate,
    DateOnly? DueDate,
    string? Currency,
    decimal? TotalAmount,
    DateTimeOffset UploadedAt,
    bool IsOverdue);
