using InvoiceFlow.Application.Abstractions.Messaging;
using InvoiceFlow.Domain.Invoices;

namespace InvoiceFlow.Application.Invoices.Upload;

/// <summary>
/// Creates an invoice from a PDF. The stream is read once; the caller must not reuse it afterwards.
/// </summary>
public sealed record UploadInvoiceCommand(
    Stream Content,
    string? FileName,
    string IdempotencyKey) : ICommand<InvoiceId>;
