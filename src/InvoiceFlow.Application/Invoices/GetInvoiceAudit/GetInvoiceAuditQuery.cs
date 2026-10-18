using InvoiceFlow.Application.Abstractions.Messaging;
using InvoiceFlow.Application.Common;
using InvoiceFlow.Application.Invoices.GetInvoice;
using InvoiceFlow.Domain.Invoices;

namespace InvoiceFlow.Application.Invoices.GetInvoiceAudit;

public sealed record GetInvoiceAuditQuery(InvoiceId Id, int Page = 1, int PageSize = 50)
    : IQuery<PagedList<InvoiceAuditEntryDto>>
{
    public const int DefaultPageSize = 50;
    public const int MaxPageSize = 100;
}

public sealed record InvoiceAuditEntryDto(
    Guid Id,
    DateTimeOffset OccurredAt,
    ActorDto Actor,
    string Action,
    IReadOnlyList<AuditChangeDto> Changes,
    string? Reason,
    string? CorrelationId);

public sealed record AuditChangeDto(string Field, string? OldValue, string? NewValue);
