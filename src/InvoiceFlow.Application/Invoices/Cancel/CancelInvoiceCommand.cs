using InvoiceFlow.Domain.Invoices;

namespace InvoiceFlow.Application.Invoices.Cancel;

public sealed record CancelInvoiceCommand(InvoiceId Id, string ExpectedVersion, string? Reason) : IInvoiceCommand;
