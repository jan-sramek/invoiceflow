using System.ComponentModel;

namespace InvoiceFlow.Domain.Errors;

/// <summary>
/// Error codes raised by the domain. Codes are part of the API contract: never rename or reuse them.
/// The description is the constant title of the error response.
/// </summary>
public static class DomainErrors
{
    public static class Validation
    {
        [Description("Invalid currency")]
        public const string CurrencyInvalid = "validation.currency_invalid";

        [Description("Too many decimal places")]
        public const string AmountPrecision = "validation.amount_precision";

        [Description("Amount out of range")]
        public const string AmountOutOfRange = "validation.amount_out_of_range";

        [Description("Value required")]
        public const string Required = "validation.required";

        [Description("Value too long")]
        public const string MaxLength = "validation.max_length";

        [Description("Amount must be positive")]
        public const string MustBePositive = "validation.must_be_positive";

        [Description("Total does not match")]
        public const string TotalMismatch = "validation.total_mismatch";

        [Description("Due date before invoice date")]
        public const string DueDateBeforeInvoiceDate = "validation.due_date_before_invoice_date";

        [Description("Date in the future")]
        public const string DateInFuture = "validation.date_in_future";
    }

    public static class Invoice
    {
        [Description("Invalid status transition")]
        public const string InvalidTransition = "invoice.invalid_transition";

        [Description("Payment date in the future")]
        public const string PaymentDateInFuture = "invoice.payment_date_in_future";

        [Description("Invoice not ready for submission")]
        public const string NotReadyForSubmission = "invoice.not_ready_for_submission";
    }

    public static class Approval
    {
        [Description("Own invoice cannot be approved")]
        public const string OwnInvoice = "approval.own_invoice";
    }
}
