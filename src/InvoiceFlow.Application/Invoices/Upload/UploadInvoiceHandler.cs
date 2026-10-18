using System.Security.Cryptography;
using InvoiceFlow.Application.Abstractions.Context;
using InvoiceFlow.Application.Abstractions.Messaging;
using InvoiceFlow.Application.Abstractions.Persistence;
using InvoiceFlow.Application.Abstractions.Storage;
using InvoiceFlow.Application.Behaviors;
using InvoiceFlow.Application.Errors;
using InvoiceFlow.Domain.Common;
using InvoiceFlow.Domain.Invoices;

namespace InvoiceFlow.Application.Invoices.Upload;

internal sealed class UploadInvoiceHandler(
    IInvoiceRepository invoices,
    IDocumentStorage storage,
    Compensations compensations,
    ITenantContext tenant,
    ICurrentUser user,
    TimeProvider time) : ICommandHandler<UploadInvoiceCommand, InvoiceId>
{
    public const string PdfContentType = "application/pdf";

    private static readonly byte[] PdfSignature = "%PDF-"u8.ToArray();

    public async Task<InvoiceId> HandleAsync(UploadInvoiceCommand command, CancellationToken cancellationToken)
    {
        Stream file = command.File!;
        if (file.Length > UploadInvoiceCommand.MaxBytes)
        {
            throw new UploadTooLargeException(UploadInvoiceCommand.MaxBytes);
        }

        if (!await StartsWithPdfSignatureAsync(file, cancellationToken))
        {
            throw new UploadNotPdfException();
        }

        Actor actor = user.Actor;
        DateTimeOffset now = time.GetUtcNow();

        file.Position = 0;
        string sha256 = Convert.ToHexStringLower(await SHA256.HashDataAsync(file, cancellationToken));
        var documentId = Guid.CreateVersion7(now);
        var document = InvoiceDocument.Create(
            documentId,
            DocumentKeys.For(tenant.TenantId, documentId),
            command.FileName,
            file.Length,
            sha256);

        file.Position = 0;
        await storage.StoreAsync(document.StorageKey, file, PdfContentType, cancellationToken);
        compensations.Add(() => storage.DeleteAsync(document.StorageKey, CancellationToken.None));

        var invoice = Invoice.Upload(actor, now, document);
        invoices.Add(invoice);
        return invoice.Id;
    }

    private static async Task<bool> StartsWithPdfSignatureAsync(Stream file, CancellationToken cancellationToken)
    {
        byte[] start = new byte[PdfSignature.Length];
        file.Position = 0;
        int read = await file.ReadAtLeastAsync(start, start.Length, throwOnEndOfStream: false, cancellationToken);
        return read == start.Length && start.AsSpan().SequenceEqual(PdfSignature);
    }
}
