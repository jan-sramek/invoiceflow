using InvoiceFlow.Domain.Errors;

namespace InvoiceFlow.Application.Errors;

public sealed class UploadTooLargeException(long maxBytes)
    : InvoiceFlowException(
        ApplicationErrors.Upload.TooLarge,
        ErrorKind.TooLarge,
        new Dictionary<string, object?> { ["maxBytes"] = maxBytes });
