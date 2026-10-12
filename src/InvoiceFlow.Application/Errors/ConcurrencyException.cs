using InvoiceFlow.Domain.Errors;

namespace InvoiceFlow.Application.Errors;

public sealed class ConcurrencyException()
    : InvoiceFlowException(ApplicationErrors.Concurrency.StaleVersion, ErrorKind.PreconditionFailed);
