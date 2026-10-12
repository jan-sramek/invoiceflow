namespace InvoiceFlow.Application.Audit;

/// <summary>
/// Audit action codes. Stored with every entry and translated in the UI: never rename or reuse them.
/// </summary>
public static class AuditActions
{
    public const string InvoiceUploaded = "invoice.uploaded";
    public const string InvoiceFieldsCorrected = "invoice.fields_corrected";
    public const string InvoiceProcessingStarted = "invoice.processing_started";
    public const string InvoiceExtracted = "invoice.extracted";
    public const string InvoiceReviewRequired = "invoice.review_required";
    public const string InvoiceExtractionFailed = "invoice.extraction_failed";
    public const string InvoiceExtractionRetried = "invoice.extraction_retried";
    public const string InvoiceManualEntryStarted = "invoice.manual_entry_started";
    public const string InvoiceSubmitted = "invoice.submitted";
    public const string InvoiceApproved = "invoice.approved";
    public const string InvoiceRejected = "invoice.rejected";
    public const string InvoiceReopened = "invoice.reopened";
    public const string InvoicePaid = "invoice.paid";
    public const string InvoiceCancelled = "invoice.cancelled";
}
