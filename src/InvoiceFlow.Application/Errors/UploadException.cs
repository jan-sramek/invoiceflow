using InvoiceFlow.Domain.Errors;

namespace InvoiceFlow.Application.Errors;

public sealed class UploadTooLargeException()
    : InvoiceFlowException(
        DomainErrors.Upload.TooLarge,
        ErrorKind.PayloadTooLarge,
        new Dictionary<string, object?> { ["maxBytes"] = Domain.Invoices.InvoiceDocument.MaxSizeBytes });

public sealed class UploadNotPdfException()
    : InvoiceFlowException(DomainErrors.Upload.NotPdf, ErrorKind.UnsupportedMediaType);
