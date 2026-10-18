using InvoiceFlow.Application.Abstractions.Context;
using InvoiceFlow.Application.Abstractions.Messaging;
using InvoiceFlow.Application.Abstractions.Persistence;
using InvoiceFlow.Application.Abstractions.Storage;
using InvoiceFlow.Application.Behaviors;
using InvoiceFlow.Application.Errors;
using InvoiceFlow.Domain.Invoices;

namespace InvoiceFlow.Application.Invoices.DownloadDocument;

internal sealed class DownloadDocumentHandler(
    IInvoiceRepository invoices,
    IDocumentStorage storage,
    Compensations compensations,
    ICurrentUser user,
    TimeProvider time) : ICommandHandler<DownloadDocumentCommand, DocumentContent>
{
    public async Task<DocumentContent> HandleAsync(DownloadDocumentCommand command, CancellationToken cancellationToken)
    {
        Invoice invoice = await invoices.GetAsync(command.Id, cancellationToken) ?? throw new InvoiceNotFoundException();
        invoice.RecordDownload(user.Actor, time.GetUtcNow());

        InvoiceDocument document = invoice.Document;
        Stream content = await storage.OpenAsync(document.StorageKey, cancellationToken);
        compensations.Add(() => content.DisposeAsync().AsTask());

        return new DocumentContent(content, document.FileName, document.SizeBytes);
    }
}
