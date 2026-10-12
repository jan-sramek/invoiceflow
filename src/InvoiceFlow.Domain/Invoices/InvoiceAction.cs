namespace InvoiceFlow.Domain.Invoices;

public enum InvoiceAction
{
    StartProcessing,
    CompleteExtraction,
    RequireReview,
    FailExtraction,
    RetryExtraction,
    EnterManually,
    UpdateFields,
    Submit,
    Approve,
    Reject,
    Reopen,
    MarkPaid,
    Cancel,
}
