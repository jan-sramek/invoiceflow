using InvoiceFlow.Domain.Errors;

namespace InvoiceFlow.Application.Errors;

public sealed class UploadNotPdfException()
    : InvoiceFlowException(ApplicationErrors.Upload.NotPdf, ErrorKind.UnsupportedMediaType);
