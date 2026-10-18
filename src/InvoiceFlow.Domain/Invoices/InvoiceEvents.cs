using InvoiceFlow.Domain.Common;

namespace InvoiceFlow.Domain.Invoices;

public sealed record InvoiceUploaded(InvoiceId InvoiceId, Actor Actor, DateTimeOffset OccurredAt, string FileName)
    : IDomainEvent;

public sealed record InvoiceDocumentDownloaded(InvoiceId InvoiceId, Actor Actor, DateTimeOffset OccurredAt)
    : IDomainEvent;

public sealed record InvoiceFieldsCorrected(
    InvoiceId InvoiceId, Actor Actor, DateTimeOffset OccurredAt, IReadOnlyList<FieldChange> Changes) : IDomainEvent;

public abstract record InvoiceStatusChanged(
    InvoiceId InvoiceId, Actor Actor, DateTimeOffset OccurredAt, InvoiceStatus From, InvoiceStatus To) : IDomainEvent;

public sealed record InvoiceProcessingStarted(
    InvoiceId InvoiceId, Actor Actor, DateTimeOffset OccurredAt, InvoiceStatus From, InvoiceStatus To)
    : InvoiceStatusChanged(InvoiceId, Actor, OccurredAt, From, To);

public sealed record InvoiceExtracted(
    InvoiceId InvoiceId, Actor Actor, DateTimeOffset OccurredAt, InvoiceStatus From, InvoiceStatus To)
    : InvoiceStatusChanged(InvoiceId, Actor, OccurredAt, From, To);

public sealed record InvoiceReviewRequired(
    InvoiceId InvoiceId, Actor Actor, DateTimeOffset OccurredAt, InvoiceStatus From, InvoiceStatus To)
    : InvoiceStatusChanged(InvoiceId, Actor, OccurredAt, From, To);

public sealed record InvoiceExtractionFailed(
    InvoiceId InvoiceId, Actor Actor, DateTimeOffset OccurredAt, InvoiceStatus From, InvoiceStatus To, string Reason)
    : InvoiceStatusChanged(InvoiceId, Actor, OccurredAt, From, To);

public sealed record InvoiceExtractionRetried(
    InvoiceId InvoiceId, Actor Actor, DateTimeOffset OccurredAt, InvoiceStatus From, InvoiceStatus To)
    : InvoiceStatusChanged(InvoiceId, Actor, OccurredAt, From, To);

public sealed record InvoiceManualEntryStarted(
    InvoiceId InvoiceId, Actor Actor, DateTimeOffset OccurredAt, InvoiceStatus From, InvoiceStatus To)
    : InvoiceStatusChanged(InvoiceId, Actor, OccurredAt, From, To);

public sealed record InvoiceSubmitted(
    InvoiceId InvoiceId, Actor Actor, DateTimeOffset OccurredAt, InvoiceStatus From, InvoiceStatus To)
    : InvoiceStatusChanged(InvoiceId, Actor, OccurredAt, From, To);

public sealed record InvoiceApproved(
    InvoiceId InvoiceId, Actor Actor, DateTimeOffset OccurredAt, InvoiceStatus From, InvoiceStatus To)
    : InvoiceStatusChanged(InvoiceId, Actor, OccurredAt, From, To);

public sealed record InvoiceRejected(
    InvoiceId InvoiceId, Actor Actor, DateTimeOffset OccurredAt, InvoiceStatus From, InvoiceStatus To, string Reason)
    : InvoiceStatusChanged(InvoiceId, Actor, OccurredAt, From, To);

public sealed record InvoiceReopened(
    InvoiceId InvoiceId, Actor Actor, DateTimeOffset OccurredAt, InvoiceStatus From, InvoiceStatus To)
    : InvoiceStatusChanged(InvoiceId, Actor, OccurredAt, From, To);

public sealed record InvoicePaid(
    InvoiceId InvoiceId,
    Actor Actor,
    DateTimeOffset OccurredAt,
    InvoiceStatus From,
    InvoiceStatus To,
    DateOnly PaidOn,
    string PaymentReference)
    : InvoiceStatusChanged(InvoiceId, Actor, OccurredAt, From, To);

public sealed record InvoiceCancelled(
    InvoiceId InvoiceId, Actor Actor, DateTimeOffset OccurredAt, InvoiceStatus From, InvoiceStatus To, string Reason)
    : InvoiceStatusChanged(InvoiceId, Actor, OccurredAt, From, To);
