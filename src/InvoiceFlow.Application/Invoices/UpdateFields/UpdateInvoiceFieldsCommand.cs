using InvoiceFlow.Application.Abstractions.Messaging;
using InvoiceFlow.Domain.Invoices;

namespace InvoiceFlow.Application.Invoices.UpdateFields;

public sealed record UpdateInvoiceFieldsCommand(InvoiceId Id, string ExpectedVersion, InvoiceFieldsInput Fields)
    : ICommand<InvoiceId>;
