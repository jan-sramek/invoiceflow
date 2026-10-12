using System.Globalization;
using System.Text.Json;
using InvoiceFlow.Domain.Common;
using InvoiceFlow.Domain.Invoices;
using InvoiceFlow.Domain.Tenants;

namespace InvoiceFlow.Application.Audit;

public static class AuditEntryFactory
{
    public const string InvoiceSubject = "invoice";

    /// <exception cref="ArgumentException">The event has no audit mapping.</exception>
    public static AuditEntry Create(IDomainEvent domainEvent, TenantId tenantId, string? correlationId)
    {
        Mapping mapping = domainEvent switch
        {
            InvoiceUploaded uploaded => new(AuditActions.InvoiceUploaded, uploaded.InvoiceId, []),
            InvoiceFieldsCorrected corrected => new(
                AuditActions.InvoiceFieldsCorrected,
                corrected.InvoiceId,
                [.. corrected.Changes.Select(c => new AuditChange(FieldName(c.Field), c.OldValue, c.NewValue))]),
            InvoiceStatusChanged changed => FromStatusChange(changed),
            _ => throw NoMapping(domainEvent),
        };

        return new AuditEntry
        {
            Id = Guid.CreateVersion7(domainEvent.OccurredAt),
            TenantId = tenantId,
            OccurredAt = domainEvent.OccurredAt,
            Actor = domainEvent.Actor,
            Action = mapping.Action,
            SubjectType = InvoiceSubject,
            SubjectId = mapping.Subject.Value,
            Changes = mapping.Changes,
            Reason = mapping.Reason,
            CorrelationId = correlationId,
        };
    }

    private static Mapping FromStatusChange(InvoiceStatusChanged changed)
    {
        AuditChange status = new("status", changed.From.ToString(), changed.To.ToString());

        return changed switch
        {
            InvoiceProcessingStarted => new(AuditActions.InvoiceProcessingStarted, changed.InvoiceId, [status]),
            InvoiceExtracted => new(AuditActions.InvoiceExtracted, changed.InvoiceId, [status]),
            InvoiceReviewRequired => new(AuditActions.InvoiceReviewRequired, changed.InvoiceId, [status]),
            InvoiceExtractionFailed failed =>
                new(AuditActions.InvoiceExtractionFailed, changed.InvoiceId, [status], failed.Reason),
            InvoiceExtractionRetried => new(AuditActions.InvoiceExtractionRetried, changed.InvoiceId, [status]),
            InvoiceManualEntryStarted => new(AuditActions.InvoiceManualEntryStarted, changed.InvoiceId, [status]),
            InvoiceSubmitted => new(AuditActions.InvoiceSubmitted, changed.InvoiceId, [status]),
            InvoiceApproved => new(AuditActions.InvoiceApproved, changed.InvoiceId, [status]),
            InvoiceRejected rejected => new(AuditActions.InvoiceRejected, changed.InvoiceId, [status], rejected.Reason),
            InvoiceReopened => new(AuditActions.InvoiceReopened, changed.InvoiceId, [status]),
            InvoicePaid paid => new(
                AuditActions.InvoicePaid,
                changed.InvoiceId,
                [
                    status,
                    new("paidOn", null, paid.PaidOn.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
                    new("paymentReference", null, paid.PaymentReference),
                ]),
            InvoiceCancelled cancelled =>
                new(AuditActions.InvoiceCancelled, changed.InvoiceId, [status], cancelled.Reason),
            _ => throw NoMapping(changed),
        };
    }

    private static string FieldName(InvoiceField field) => JsonNamingPolicy.CamelCase.ConvertName(field.ToString());

    private static ArgumentException NoMapping(IDomainEvent domainEvent) =>
        new($"No audit mapping for {domainEvent.GetType().Name}.", nameof(domainEvent));

    private sealed record Mapping(
        string Action, InvoiceId Subject, IList<AuditChange> Changes, string? Reason = null);
}
