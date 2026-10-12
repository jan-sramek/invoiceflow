using InvoiceFlow.Domain.Invoices;

namespace InvoiceFlow.Domain.Errors;

public sealed class InvoiceNotReadyException(IReadOnlyList<FieldIssue> issues)
    : InvoiceFlowException(DomainErrors.Invoice.NotReadyForSubmission, ErrorKind.BusinessRule)
{
    public IReadOnlyList<FieldIssue> Issues { get; } = issues;
}
