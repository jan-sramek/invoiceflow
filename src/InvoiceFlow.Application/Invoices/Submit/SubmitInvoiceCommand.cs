using InvoiceFlow.Application.Abstractions.Messaging;
using InvoiceFlow.Domain.Invoices;

namespace InvoiceFlow.Application.Invoices.Submit;

public sealed record SubmitInvoiceCommand(InvoiceId Id, string ExpectedVersion) : ICommand<InvoiceId>;
