using InvoiceFlow.Domain.Common;
using InvoiceFlow.Domain.Invoices;

namespace InvoiceFlow.Application.Invoices.GetInvoice;

public sealed record InvoiceDetailDto(
    Guid Id,
    InvoiceStatus Status,
    InvoiceFieldsDto Fields,
    ActorDto UploadedBy,
    DateTimeOffset UploadedAt,
    ActorDto? SubmittedBy,
    DateOnly? PaidOn,
    string? PaymentReference);

/// <summary>
/// The editable fields under the same names the edit request uses. Any of them may be missing until submission.
/// </summary>
public sealed record InvoiceFieldsDto(
    string? SupplierName,
    string? InvoiceNumber,
    DateOnly? InvoiceDate,
    DateOnly? DueDate,
    string? Currency,
    decimal? Subtotal,
    decimal? TaxAmount,
    decimal? TotalAmount,
    string? PurchaseOrderReference);

public sealed record ActorDto(ActorType Type, string Id, string Name);
