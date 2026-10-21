using InvoiceFlow.Application.Abstractions.Documents;
using InvoiceFlow.Application.Abstractions.Extraction;
using InvoiceFlow.Application.Abstractions.Persistence;
using InvoiceFlow.Domain.Common;
using InvoiceFlow.Domain.Invoices;

namespace InvoiceFlow.Worker;

/// <summary>
/// Loads an uploaded invoice, extracts fields, and moves it to review or extraction-failed.
/// Technical failures (storage/HTTP/DB) are rethrown so the consumer can route to TTL retry / DLQ.
/// Successful or business outcomes record the RabbitMQ message id in <c>processed_messages</c>.
/// </summary>
internal sealed partial class InvoiceProcessingService(
    IInvoiceRepository invoices,
    IDocumentStorage storage,
    IInvoiceExtractor extractor,
    IProcessedMessageStore processedMessages,
    IUnitOfWork unitOfWork,
    TimeProvider time,
    ILogger<InvoiceProcessingService> logger)
{
    private static readonly Actor WorkerActor = Actor.System("invoiceflow-worker");

    public async Task ProcessAsync(Guid messageId, InvoiceId invoiceId, CancellationToken cancellationToken)
    {
        if (await processedMessages.ExistsAsync(messageId, cancellationToken))
        {
            LogDuplicateDelivery(logger, messageId, invoiceId.Value);
            return;
        }

        Invoice? invoice = await invoices.GetAsync(invoiceId, cancellationToken);
        if (invoice is null)
        {
            LogNotFound(logger, invoiceId.Value);
            return;
        }

        if (invoice.Status is not (InvoiceStatus.Uploaded or InvoiceStatus.Processing))
        {
            LogAlreadyProcessed(logger, invoiceId.Value, invoice.Status);
            // Still record the message so redeliveries short-circuit without re-checking status.
            processedMessages.Add(messageId, time.GetUtcNow());
            await unitOfWork.CommitAsync(cancellationToken);
            return;
        }

        DateTimeOffset now = time.GetUtcNow();
        if (invoice.Status == InvoiceStatus.Uploaded)
        {
            invoice.StartProcessing(WorkerActor, now);
        }

        await using Stream pdf = await storage.OpenReadAsync(invoice.Document.StorageKey, cancellationToken);
        ExtractionResult result = await extractor.ExtractAsync(pdf, cancellationToken);

        switch (result)
        {
            case ExtractionResult.Succeeded succeeded:
                invoice.RequireReview(WorkerActor, time.GetUtcNow());
                invoice.ApplyExtraction(
                    WorkerActor,
                    time.GetUtcNow(),
                    succeeded.Fields,
                    succeeded.Statuses,
                    succeeded.DocumentLanguage);
                break;
            case ExtractionResult.Failed failed:
                FailProcessing(invoice, failed.Code);
                break;
            default:
                throw new InvalidOperationException($"Unexpected extraction result {result.GetType().Name}.");
        }

        processedMessages.Add(messageId, time.GetUtcNow());
        await unitOfWork.CommitAsync(cancellationToken);
    }

    public async Task FailAfterRetriesAsync(
        Guid messageId, InvoiceId invoiceId, string reason, CancellationToken cancellationToken)
    {
        if (await processedMessages.ExistsAsync(messageId, cancellationToken))
        {
            LogDuplicateDelivery(logger, messageId, invoiceId.Value);
            return;
        }

        Invoice? invoice = await invoices.GetAsync(invoiceId, cancellationToken);
        if (invoice is null)
        {
            processedMessages.Add(messageId, time.GetUtcNow());
            await unitOfWork.CommitAsync(cancellationToken);
            return;
        }

        if (invoice.Status is InvoiceStatus.Uploaded)
        {
            invoice.StartProcessing(WorkerActor, time.GetUtcNow());
        }

        if (invoice.Status is InvoiceStatus.Processing)
        {
            FailProcessing(invoice, reason);
        }

        processedMessages.Add(messageId, time.GetUtcNow());
        await unitOfWork.CommitAsync(cancellationToken);
    }

    private void FailProcessing(Invoice invoice, string reason)
    {
        if (invoice.Status == InvoiceStatus.Processing)
        {
            invoice.FailExtraction(WorkerActor, time.GetUtcNow(), reason);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Invoice {InvoiceId} not found; acknowledging message")]
    private static partial void LogNotFound(ILogger logger, Guid invoiceId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Invoice {InvoiceId} already in status {Status}; skipping")]
    private static partial void LogAlreadyProcessed(ILogger logger, Guid invoiceId, InvoiceStatus status);

    [LoggerMessage(Level = LogLevel.Information, Message = "Skipping duplicate delivery of message {MessageId} for invoice {InvoiceId}")]
    private static partial void LogDuplicateDelivery(ILogger logger, Guid messageId, Guid invoiceId);
}
