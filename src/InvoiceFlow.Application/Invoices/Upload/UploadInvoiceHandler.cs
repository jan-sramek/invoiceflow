using System.Buffers;
using System.Security.Cryptography;
using InvoiceFlow.Application.Abstractions.Context;
using InvoiceFlow.Application.Abstractions.Documents;
using InvoiceFlow.Application.Abstractions.Messaging;
using InvoiceFlow.Application.Abstractions.Persistence;
using InvoiceFlow.Application.Errors;
using InvoiceFlow.Domain.Invoices;

namespace InvoiceFlow.Application.Invoices.Upload;

internal sealed class UploadInvoiceHandler(
    IDocumentStorage storage,
    IInvoiceRepository invoices,
    IIdempotencyStore idempotency,
    ITenantContext tenant,
    ICurrentUser user,
    IInvoiceEventPublisher events,
    TimeProvider time) : ICommandHandler<UploadInvoiceCommand, InvoiceId>
{
    public async Task<InvoiceId> HandleAsync(UploadInvoiceCommand command, CancellationToken cancellationToken)
    {
        (byte[] bytes, string sha256) = await ReadPdfAsync(command.Content, cancellationToken);
        string key = command.IdempotencyKey.Trim();
        string userId = user.Actor.Id;

        IdempotencyBeginResult begin = await idempotency.BeginAsync(key, userId, sha256, cancellationToken);
        switch (begin)
        {
            case IdempotencyBeginResult.Completed completed:
                return new InvoiceId(completed.InvoiceId);
            case IdempotencyBeginResult.InProgress:
                throw new IdempotencyInProgressException();
            case IdempotencyBeginResult.KeyReused:
                throw new IdempotencyKeyReusedException();
            case IdempotencyBeginResult.Started:
                break;
            default:
                throw new InvalidOperationException($"Unexpected idempotency result {begin.GetType().Name}.");
        }

        DateTimeOffset now = time.GetUtcNow();
        var documentId = Guid.CreateVersion7(now);
        string storageKey = $"{tenant.TenantId.Value:N}/{documentId:N}";

        try
        {
            await using (var content = new MemoryStream(bytes, writable: false))
            {
                await storage.PutAsync(
                    storageKey, content, InvoiceDocument.PdfContentType, bytes.LongLength, cancellationToken);
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException and not StorageUnavailableException)
        {
            await idempotency.AbandonAsync(key, userId, CancellationToken.None);
            throw new StorageUnavailableException(exception);
        }
        catch
        {
            await idempotency.AbandonAsync(key, userId, CancellationToken.None);
            throw;
        }

        InvoiceId? duplicateOf = await invoices.FindIdByDocumentSha256Async(sha256, cancellationToken);
        var document = InvoiceDocument.Create(documentId, storageKey, command.FileName, bytes.LongLength, sha256);
        var invoice = Invoice.Upload(user.Actor, now, document, duplicateOf);
        invoices.Add(invoice);
        await idempotency.CompleteAsync(key, userId, invoice.Id.Value, cancellationToken);

        await events.PublishUploadedAsync(tenant.TenantId, invoice.Id, cancellationToken);

        return invoice.Id;
    }

    private static async Task<(byte[] Bytes, string Sha256)> ReadPdfAsync(Stream content, CancellationToken cancellationToken)
    {
        byte[] buffer = ArrayPool<byte>.Shared.Rent((int)InvoiceDocument.MaxSizeBytes + 1);
        try
        {
            int total = 0;
            while (true)
            {
                int read = await content.ReadAsync(buffer.AsMemory(total, buffer.Length - total), cancellationToken);
                if (read == 0)
                {
                    break;
                }

                total += read;
                if (total > InvoiceDocument.MaxSizeBytes)
                {
                    throw new UploadTooLargeException();
                }
            }

            if (total < InvoiceDocument.PdfMagic.Length
                || !buffer.AsSpan(0, InvoiceDocument.PdfMagic.Length).SequenceEqual(InvoiceDocument.PdfMagic))
            {
                throw new UploadNotPdfException();
            }

            byte[] bytes = buffer.AsSpan(0, total).ToArray();
            string sha256 = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
            return (bytes, sha256);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }
}