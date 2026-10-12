namespace InvoiceFlow.Domain.Errors;

/// <summary>
/// Error codes raised by the domain. Codes are part of the API contract: never rename or reuse them.
/// </summary>
public static class DomainErrors
{
    public static class Validation
    {
        public const string CurrencyInvalid = "validation.currency_invalid";
        public const string AmountPrecision = "validation.amount_precision";
        public const string AmountOutOfRange = "validation.amount_out_of_range";
        public const string Required = "validation.required";
        public const string MaxLength = "validation.max_length";
        public const string MustBePositive = "validation.must_be_positive";
        public const string TotalMismatch = "validation.total_mismatch";
        public const string DueDateBeforeInvoiceDate = "validation.due_date_before_invoice_date";
        public const string DateInFuture = "validation.date_in_future";
    }

    public static class Invoice
    {
        public const string InvalidTransition = "invoice.invalid_transition";
        public const string PaymentDateInFuture = "invoice.payment_date_in_future";
        public const string NotReadyForSubmission = "invoice.not_ready_for_submission";
    }

    public static class Approval
    {
        public const string OwnInvoice = "approval.own_invoice";
    }
}
