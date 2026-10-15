using InvoiceFlow.Domain.Invoices;

namespace InvoiceFlow.Application.Invoices.Approve;

public sealed record ApproveInvoiceCommand(InvoiceId Id, string ExpectedVersion) : IInvoiceCommand;
