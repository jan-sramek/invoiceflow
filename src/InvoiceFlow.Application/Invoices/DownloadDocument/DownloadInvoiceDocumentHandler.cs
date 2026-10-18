using InvoiceFlow.Application.Abstractions.Context;
using InvoiceFlow.Application.Abstractions.Messaging;
using InvoiceFlow.Application.Abstractions.Persistence;
using InvoiceFlow.Application.Errors;
using InvoiceFlow.Domain.Invoices;

namespace InvoiceFlow.Application.Invoices.DownloadDocument;

internal sealed class DownloadInvoiceDocumentHandler(
    IInvoiceRepository invoices, ICurrentUser user, TimeProvider time)
    : ICommandHandler<DownloadInvoiceDocumentCommand, InvoiceDocumentAccess>
{
    public async Task<InvoiceDocumentAccess> HandleAsync(
        DownloadInvoiceDocumentCommand command, CancellationToken cancellationToken)
    {
        Invoice invoice = await invoices.GetAsync(command.Id, cancellationToken)
            ?? throw new InvoiceNotFoundException();

        invoice.RecordDocumentDownloaded(user.Actor, time.GetUtcNow());
        InvoiceDocument document = invoice.Document;

        return new InvoiceDocumentAccess(
            document.StorageKey, document.OriginalFileName, document.ContentType, document.SizeBytes);
    }
}