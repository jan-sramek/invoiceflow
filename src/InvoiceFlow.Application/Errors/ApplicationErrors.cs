namespace InvoiceFlow.Application.Errors;

/// <summary>
/// Error codes raised by the application layer. Codes are part of the API contract: never rename or reuse them.
/// </summary>
public static class ApplicationErrors
{
    public static class Concurrency
    {
        public const string StaleVersion = "concurrency.stale_version";
    }
}
