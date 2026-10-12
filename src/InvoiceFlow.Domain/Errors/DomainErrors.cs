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
    }
}
