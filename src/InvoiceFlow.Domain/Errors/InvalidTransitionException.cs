using InvoiceFlow.Domain.Invoices;

namespace InvoiceFlow.Domain.Errors;

public sealed class InvalidTransitionException(InvoiceStatus from, InvoiceAction action)
    : InvoiceFlowException(
        DomainErrors.Invoice.InvalidTransition,
        ErrorKind.Conflict,
        new Dictionary<string, object?> { ["from"] = from.ToString(), ["action"] = action.ToString() })
{
    public InvoiceStatus From { get; } = from;

    public InvoiceAction Action { get; } = action;
}
