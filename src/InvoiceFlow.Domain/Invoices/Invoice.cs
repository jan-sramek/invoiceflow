using System.Collections.Frozen;
using InvoiceFlow.Domain.Common;
using InvoiceFlow.Domain.Errors;

namespace InvoiceFlow.Domain.Invoices;

public sealed class Invoice : AggregateRoot
{
    public const int MaxReasonLength = 1000;
    public const int MaxPaymentReferenceLength = 100;

    // The latest local time anywhere is UTC+14, so a date that is today for any user is never in the future.
    private static readonly TimeSpan LatestUtcOffset = TimeSpan.FromHours(14);

    private static readonly FrozenDictionary<(InvoiceStatus From, InvoiceAction Action), InvoiceStatus> Transitions =
        new Dictionary<(InvoiceStatus From, InvoiceAction Action), InvoiceStatus>
        {
            [(InvoiceStatus.Uploaded, InvoiceAction.StartProcessing)] = InvoiceStatus.Processing,
            [(InvoiceStatus.Uploaded, InvoiceAction.EnterManually)] = InvoiceStatus.NeedsReview,
            [(InvoiceStatus.Uploaded, InvoiceAction.Cancel)] = InvoiceStatus.Cancelled,

            [(InvoiceStatus.Processing, InvoiceAction.CompleteExtraction)] = InvoiceStatus.Extracted,
            [(InvoiceStatus.Processing, InvoiceAction.RequireReview)] = InvoiceStatus.NeedsReview,
            [(InvoiceStatus.Processing, InvoiceAction.FailExtraction)] = InvoiceStatus.ExtractionFailed,

            [(InvoiceStatus.ExtractionFailed, InvoiceAction.RetryExtraction)] = InvoiceStatus.Processing,
            [(InvoiceStatus.ExtractionFailed, InvoiceAction.EnterManually)] = InvoiceStatus.NeedsReview,
            [(InvoiceStatus.ExtractionFailed, InvoiceAction.Cancel)] = InvoiceStatus.Cancelled,

            [(InvoiceStatus.Extracted, InvoiceAction.UpdateFields)] = InvoiceStatus.Extracted,
            [(InvoiceStatus.Extracted, InvoiceAction.Submit)] = InvoiceStatus.PendingApproval,
            [(InvoiceStatus.Extracted, InvoiceAction.Cancel)] = InvoiceStatus.Cancelled,

            [(InvoiceStatus.NeedsReview, InvoiceAction.UpdateFields)] = InvoiceStatus.NeedsReview,
            [(InvoiceStatus.NeedsReview, InvoiceAction.Submit)] = InvoiceStatus.PendingApproval,
            [(InvoiceStatus.NeedsReview, InvoiceAction.Cancel)] = InvoiceStatus.Cancelled,

            [(InvoiceStatus.PendingApproval, InvoiceAction.Approve)] = InvoiceStatus.Approved,
            [(InvoiceStatus.PendingApproval, InvoiceAction.Reject)] = InvoiceStatus.Rejected,
            [(InvoiceStatus.PendingApproval, InvoiceAction.Cancel)] = InvoiceStatus.Cancelled,

            [(InvoiceStatus.Rejected, InvoiceAction.Reopen)] = InvoiceStatus.NeedsReview,
            [(InvoiceStatus.Rejected, InvoiceAction.Cancel)] = InvoiceStatus.Cancelled,

            [(InvoiceStatus.Approved, InvoiceAction.MarkPaid)] = InvoiceStatus.Paid,
        }.ToFrozenDictionary();

    // Used by EF Core when loading.
    private Invoice()
    {
        UploadedBy = null!;
        Document = null!;
    }

    private Invoice(InvoiceId id, Actor uploadedBy, DateTimeOffset uploadedAt, InvoiceDocument document)
    {
        Id = id;
        Status = InvoiceStatus.Uploaded;
        UploadedBy = uploadedBy;
        UploadedAt = uploadedAt;
        Document = document;
    }

    public InvoiceId Id { get; }

    public InvoiceStatus Status { get; private set; }

    public Actor UploadedBy { get; }

    public DateTimeOffset UploadedAt { get; }

    public InvoiceDocument Document { get; }

    public InvoiceFields Fields { get; private set; } = InvoiceFields.Empty;

    public Actor? SubmittedBy { get; private set; }

    public DateOnly? PaidOn { get; private set; }

    public string? PaymentReference { get; private set; }

    public static Invoice Upload(Actor uploadedBy, DateTimeOffset now, InvoiceDocument document)
    {
        var invoice = new Invoice(InvoiceId.New(now), uploadedBy, now, document);
        invoice.Raise(new InvoiceUploaded(invoice.Id, uploadedBy, now, document.FileName));
        return invoice;
    }

    /// <summary>
    /// Downloads change nothing, but who read the document and when is part of the invoice's history.
    /// </summary>
    public void RecordDownload(Actor actor, DateTimeOffset now) =>
        Raise(new InvoiceDocumentDownloaded(Id, actor, now));

    public void StartProcessing(Actor actor, DateTimeOffset now)
    {
        InvoiceStatus next = NextStatus(InvoiceAction.StartProcessing);
        MoveTo(new InvoiceProcessingStarted(Id, actor, now, Status, next));
    }

    public void CompleteExtraction(Actor actor, DateTimeOffset now)
    {
        InvoiceStatus next = NextStatus(InvoiceAction.CompleteExtraction);
        MoveTo(new InvoiceExtracted(Id, actor, now, Status, next));
    }

    public void RequireReview(Actor actor, DateTimeOffset now)
    {
        InvoiceStatus next = NextStatus(InvoiceAction.RequireReview);
        MoveTo(new InvoiceReviewRequired(Id, actor, now, Status, next));
    }

    public void FailExtraction(Actor actor, DateTimeOffset now, string reason)
    {
        InvoiceStatus next = NextStatus(InvoiceAction.FailExtraction);
        string validReason = Text.Require(reason, nameof(reason), MaxReasonLength);
        MoveTo(new InvoiceExtractionFailed(Id, actor, now, Status, next, validReason));
    }

    public void RetryExtraction(Actor actor, DateTimeOffset now)
    {
        InvoiceStatus next = NextStatus(InvoiceAction.RetryExtraction);
        MoveTo(new InvoiceExtractionRetried(Id, actor, now, Status, next));
    }

    public void EnterManually(Actor actor, DateTimeOffset now)
    {
        InvoiceStatus next = NextStatus(InvoiceAction.EnterManually);
        MoveTo(new InvoiceManualEntryStarted(Id, actor, now, Status, next));
    }

    public void UpdateFields(Actor actor, DateTimeOffset now, InvoiceFields fields)
    {
        NextStatus(InvoiceAction.UpdateFields);
        IReadOnlyList<FieldChange> changes = fields.ChangesFrom(Fields);

        if (changes.Count == 0)
        {
            return;
        }

        Fields = fields;
        Raise(new InvoiceFieldsCorrected(Id, actor, now, changes));
    }

    public IReadOnlyList<FieldIssue> CheckReadiness(DateTimeOffset now) => Fields.FindIssues(LatestToday(now));

    public void Submit(Actor actor, DateTimeOffset now)
    {
        InvoiceStatus next = NextStatus(InvoiceAction.Submit);
        IReadOnlyList<FieldIssue> issues = CheckReadiness(now);

        if (issues.Count > 0)
        {
            throw new InvoiceNotReadyException(issues);
        }

        SubmittedBy = actor;
        MoveTo(new InvoiceSubmitted(Id, actor, now, Status, next));
    }

    public void Approve(Actor actor, DateTimeOffset now)
    {
        InvoiceStatus next = NextStatus(InvoiceAction.Approve);
        EnsureNotSubmittedBy(actor);
        MoveTo(new InvoiceApproved(Id, actor, now, Status, next));
    }

    public void Reject(Actor actor, DateTimeOffset now, string reason)
    {
        InvoiceStatus next = NextStatus(InvoiceAction.Reject);
        EnsureNotSubmittedBy(actor);
        string validReason = Text.Require(reason, nameof(reason), MaxReasonLength);
        MoveTo(new InvoiceRejected(Id, actor, now, Status, next, validReason));
    }

    public void Reopen(Actor actor, DateTimeOffset now)
    {
        InvoiceStatus next = NextStatus(InvoiceAction.Reopen);
        MoveTo(new InvoiceReopened(Id, actor, now, Status, next));
    }

    public void MarkPaid(Actor actor, DateTimeOffset now, DateOnly paidOn, string paymentReference)
    {
        InvoiceStatus next = NextStatus(InvoiceAction.MarkPaid);
        string validReference = Text.Require(paymentReference, nameof(paymentReference), MaxPaymentReferenceLength);

        if (paidOn > LatestToday(now))
        {
            throw new BusinessRuleException(DomainErrors.Invoice.PaymentDateInFuture);
        }

        PaidOn = paidOn;
        PaymentReference = validReference;
        MoveTo(new InvoicePaid(Id, actor, now, Status, next, paidOn, validReference));
    }

    public void Cancel(Actor actor, DateTimeOffset now, string reason)
    {
        InvoiceStatus next = NextStatus(InvoiceAction.Cancel);
        string validReason = Text.Require(reason, nameof(reason), MaxReasonLength);
        MoveTo(new InvoiceCancelled(Id, actor, now, Status, next, validReason));
    }

    private static DateOnly LatestToday(DateTimeOffset now) => DateOnly.FromDateTime(now.ToOffset(LatestUtcOffset).DateTime);

    private InvoiceStatus NextStatus(InvoiceAction action) =>
        Transitions.TryGetValue((Status, action), out InvoiceStatus next)
            ? next
            : throw new InvalidTransitionException(Status, action);

    private void EnsureNotSubmittedBy(Actor actor)
    {
        if (SubmittedBy?.IsSameAs(actor) == true)
        {
            throw new ForbiddenException(DomainErrors.Approval.OwnInvoice);
        }
    }

    private void MoveTo(InvoiceStatusChanged change)
    {
        Raise(change);
        Status = change.To;
    }
}
