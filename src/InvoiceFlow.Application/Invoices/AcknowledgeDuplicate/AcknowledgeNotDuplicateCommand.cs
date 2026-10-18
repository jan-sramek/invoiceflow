using InvoiceFlow.Domain.Invoices;

namespace InvoiceFlow.Application.Invoices.AcknowledgeDuplicate;

public sealed record AcknowledgeNotDuplicateCommand(InvoiceId Id, string ExpectedVersion, string? Reason)
    : IInvoiceCommand;
