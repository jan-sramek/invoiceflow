namespace InvoiceFlow.Api.Invoices;

/// <summary>
/// A payment in full; the date may not be later than today.
/// </summary>
public sealed record PaymentRequest(DateOnly? PaidOn, string? PaymentReference);
