using InvoiceFlow.Domain.Common;
using InvoiceFlow.Domain.Tenants;

namespace InvoiceFlow.Application.Audit;

public sealed record AuditEntry
{
    public required Guid Id { get; init; }

    public required TenantId TenantId { get; init; }

    public required DateTimeOffset OccurredAt { get; init; }

    public required Actor Actor { get; init; }

    public required string Action { get; init; }

    public required string SubjectType { get; init; }

    public required Guid SubjectId { get; init; }

    public required IList<AuditChange> Changes { get; init; }

    public string? Reason { get; init; }

    public string? CorrelationId { get; init; }
}

public sealed record AuditChange(string Field, string? OldValue, string? NewValue);
