using InvoiceFlow.Application.Abstractions.Messaging;
using InvoiceFlow.Domain.Invoices;

namespace InvoiceFlow.Application.Invoices.EnterManually;

public sealed record EnterInvoiceManuallyCommand(InvoiceId Id, string ExpectedVersion) : ICommand<InvoiceId>;
