using InvoiceFlow.Application.Abstractions.Documents;
using InvoiceFlow.Application.Abstractions.Extraction;
using InvoiceFlow.Application.Abstractions.Persistence;
using InvoiceFlow.Domain.Common;
using InvoiceFlow.Domain.Invoices;

namespace InvoiceFlow.Worker;

/// <summary>
/// Loads an uploaded invoice, extracts fields, and moves it to review or extraction-failed.
/// </summary>
internal sealed partial class InvoiceProcessingService(
    IInvoiceRepository invoices,
    IDocumentStorage storage,
    IInvoiceExtractor extractor,
    IUnitOfWork unitOfWork,
    TimeProvider time,
    ILogger<InvoiceProcessingService> logger)
{
    private static readonly Actor WorkerActor = Actor.System("invoiceflow-worker");

    public async Task ProcessAsync(InvoiceId invoiceId, CancellationToken cancellationToken)
    {
        Invoice? invoice = await invoices.GetAsync(invoiceId, cancellationToken);
        if (invoice is null)
        {
            LogNotFound(logger, invoiceId.Value);
            return;
        }

        if (invoice.Status is not (InvoiceStatus.Uploaded or InvoiceStatus.Processing))
        {
            LogAlreadyProcessed(logger, invoiceId.Value, invoice.Status);
            return;
        }

        DateTimeOffset now = time.GetUtcNow();
        if (invoice.Status == InvoiceStatus.Uploaded)
        {
            invoice.StartProcessing(WorkerActor, now);
        }

        try
        {
            await using Stream pdf = await storage.OpenReadAsync(invoice.Document.StorageKey, cancellationToken);
            InvoiceFields fields = await extractor.ExtractAsync(pdf, cancellationToken);

            invoice.RequireReview(WorkerActor, time.GetUtcNow());
            if (fields != InvoiceFields.Empty)
            {
                invoice.UpdateFields(WorkerActor, time.GetUtcNow(), fields);
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogExtractionFailed(logger, exception, invoiceId.Value);
            FailProcessing(invoice, ShortReason(exception));
        }

        await unitOfWork.CommitAsync(cancellationToken);
    }

    public async Task FailAfterRetriesAsync(InvoiceId invoiceId, string reason, CancellationToken cancellationToken)
    {
        Invoice? invoice = await invoices.GetAsync(invoiceId, cancellationToken);
        if (invoice is null)
        {
            return;
        }

        if (invoice.Status is InvoiceStatus.Uploaded)
        {
            invoice.StartProcessing(WorkerActor, time.GetUtcNow());
        }

        if (invoice.Status is InvoiceStatus.Processing)
        {
            FailProcessing(invoice, reason);
            await unitOfWork.CommitAsync(cancellationToken);
        }
    }

    private void FailProcessing(Invoice invoice, string reason)
    {
        if (invoice.Status == InvoiceStatus.Processing)
        {
            invoice.FailExtraction(WorkerActor, time.GetUtcNow(), reason);
        }
    }

    private static string ShortReason(Exception exception)
    {
        string message = exception.GetBaseException().Message;
        if (string.IsNullOrWhiteSpace(message))
        {
            return "Extraction failed";
        }

        return message.Length <= Invoice.MaxReasonLength ? message : message[..Invoice.MaxReasonLength];
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Invoice {InvoiceId} not found; acknowledging message")]
    private static partial void LogNotFound(ILogger logger, Guid invoiceId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Invoice {InvoiceId} already in status {Status}; skipping")]
    private static partial void LogAlreadyProcessed(ILogger logger, Guid invoiceId, InvoiceStatus status);

    [LoggerMessage(Level = LogLevel.Error, Message = "Extraction failed for invoice {InvoiceId}")]
    private static partial void LogExtractionFailed(ILogger logger, Exception exception, Guid invoiceId);
}
