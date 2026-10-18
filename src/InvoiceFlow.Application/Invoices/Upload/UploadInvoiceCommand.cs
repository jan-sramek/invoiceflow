using InvoiceFlow.Application.Abstractions.Messaging;
using InvoiceFlow.Domain.Invoices;

namespace InvoiceFlow.Application.Invoices.Upload;

/// <summary>
/// <see cref="File"/> must be seekable: it is read for its checks and hash, then again to store it.
/// </summary>
public sealed record UploadInvoiceCommand(Stream? File, string? FileName) : ICommand<InvoiceId>
{
    public const long MaxBytes = 10 * 1024 * 1024;
}
