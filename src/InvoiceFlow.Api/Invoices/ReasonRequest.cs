namespace InvoiceFlow.Api.Invoices;

/// <summary>
/// Why an invoice is rejected or cancelled; required and kept in its history.
/// </summary>
public sealed record ReasonRequest(string? Reason);
