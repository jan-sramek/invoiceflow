namespace InvoiceFlow.Domain.Invoices;

public enum InvoiceStatus
{
    Uploaded,
    Processing,
    Extracted,
    NeedsReview,
    ExtractionFailed,
    PendingApproval,
    Approved,
    Rejected,
    Paid,
    Cancelled,
}
