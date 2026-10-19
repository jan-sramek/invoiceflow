using InvoiceFlow.Domain.Invoices;

namespace InvoiceFlow.Application.Invoices.RetryExtraction;

public sealed record RetryExtractionCommand(InvoiceId Id, string ExpectedVersion) : IInvoiceCommand;
