using System.ComponentModel;

namespace InvoiceFlow.Application.Errors;

/// <summary>
/// Error codes raised by the application layer. Codes are part of the API contract: never rename or reuse them.
/// The description is the constant title of the error response.
/// </summary>
public static class ApplicationErrors
{
    public static class Validation
    {
        /// <summary>
        /// The request has field errors; each one carries its own code.
        /// </summary>
        [Description("Validation failed")]
        public const string Failed = "validation.failed";

        [Description("Value out of range")]
        public const string OutOfRange = "validation.out_of_range";

        [Description("Range ends before it starts")]
        public const string RangeReversed = "validation.range_reversed";

        [Description("Value not allowed")]
        public const string NotAllowed = "validation.not_allowed";
    }

    public static class Auth
    {
        [Description("Sign-in required")]
        public const string UserRequired = "auth.user_required";
    }

    public static class Invoice
    {
        /// <summary>
        /// Also returned for invoices of other tenants, so the response never confirms that an id exists.
        /// </summary>
        [Description("Invoice not found")]
        public const string NotFound = "invoice.not_found";
    }

    public static class Concurrency
    {
        [Description("Changed by someone else")]
        public const string StaleVersion = "concurrency.stale_version";
    }

    public static class Tenant
    {
        [Description("Tenant unavailable")]
        public const string Unavailable = "tenant.unavailable";
    }

    public static class Storage
    {
        [Description("Document storage unavailable")]
        public const string Unavailable = "storage.unavailable";
    }

    public static class Idempotency
    {
        [Description("Idempotency key in progress")]
        public const string InProgress = "idempotency.in_progress";

        [Description("Idempotency key reused")]
        public const string KeyReused = "idempotency.key_reused";

        [Description("Idempotency key required")]
        public const string KeyRequired = "idempotency.key_required";
    }
}
