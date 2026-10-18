using InvoiceFlow.Domain.Errors;

namespace InvoiceFlow.Application.Errors;

/// <summary>
/// Documents cannot be stored or read right now; nothing was changed.
/// </summary>
public sealed class StorageUnavailableException(Exception? innerException = null)
    : InvoiceFlowException(ApplicationErrors.Storage.Unavailable, ErrorKind.Unavailable, innerException: innerException);
