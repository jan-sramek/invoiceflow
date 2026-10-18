using InvoiceFlow.Domain.Errors;

namespace InvoiceFlow.Application.Errors;

public sealed class StorageUnavailableException(Exception? innerException = null)
    : InvoiceFlowException(ApplicationErrors.Storage.Unavailable, ErrorKind.Unavailable, innerException: innerException);
