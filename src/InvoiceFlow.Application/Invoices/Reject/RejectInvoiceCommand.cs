using InvoiceFlow.Domain.Invoices;

namespace InvoiceFlow.Application.Invoices.Reject;

public sealed record RejectInvoiceCommand(InvoiceId Id, string ExpectedVersion, string? Reason) : IInvoiceCommand;
