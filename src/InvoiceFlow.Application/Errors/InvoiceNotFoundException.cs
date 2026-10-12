using InvoiceFlow.Domain.Errors;

namespace InvoiceFlow.Application.Errors;

public sealed class InvoiceNotFoundException()
    : InvoiceFlowException(ApplicationErrors.Invoice.NotFound, ErrorKind.NotFound);
