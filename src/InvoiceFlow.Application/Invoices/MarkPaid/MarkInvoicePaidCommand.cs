using InvoiceFlow.Domain.Invoices;

namespace InvoiceFlow.Application.Invoices.MarkPaid;

public sealed record MarkInvoicePaidCommand(
    InvoiceId Id,
    string ExpectedVersion,
    DateOnly? PaidOn,
    string? PaymentReference) : IInvoiceCommand;
