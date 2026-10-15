using InvoiceFlow.Domain.Invoices;

namespace InvoiceFlow.Application.Invoices.Reopen;

public sealed record ReopenInvoiceCommand(InvoiceId Id, string ExpectedVersion) : IInvoiceCommand;
