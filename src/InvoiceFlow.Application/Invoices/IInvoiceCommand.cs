using InvoiceFlow.Application.Abstractions.Messaging;
using InvoiceFlow.Domain.Invoices;

namespace InvoiceFlow.Application.Invoices;

/// <summary>
/// A change to one existing invoice, allowed only while it still has the version the client last read.
/// </summary>
public interface IInvoiceCommand : ICommand<InvoiceId>
{
    InvoiceId Id { get; }

    string ExpectedVersion { get; }
}
