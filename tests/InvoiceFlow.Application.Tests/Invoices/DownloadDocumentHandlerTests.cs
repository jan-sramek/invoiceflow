using InvoiceFlow.Application.Abstractions.Context;
using InvoiceFlow.Application.Abstractions.Persistence;
using InvoiceFlow.Application.Behaviors;
using InvoiceFlow.Application.Errors;
using InvoiceFlow.Application.Invoices.DownloadDocument;
using InvoiceFlow.Domain.Common;
using InvoiceFlow.Domain.Invoices;
using InvoiceFlow.Testing.Storage;
using Microsoft.Extensions.Logging.Abstractions;

namespace InvoiceFlow.Application.Tests.Invoices;

public class DownloadDocumentHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 15, 12, 0, 0, TimeSpan.Zero);
    private static readonly Actor Clerk = Actor.User("clerk-1", "Clara Clerk");
    private static readonly byte[] Pdf = "%PDF-1.4\n"u8.ToArray();

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Handle_KnownInvoice_ReturnsContentAndRecordsDownload()
    {
        var document = InvoiceDocument.Create(
            Guid.CreateVersion7(Now), "tenants/t/documents/d", "FV-1.pdf", Pdf.Length, new string('a', 64));
        var invoice = Invoice.Upload(Clerk, Now, document);
        invoice.ClearDomainEvents();
        var storage = new InMemoryDocumentStorage();
        await storage.StoreAsync(document.StorageKey, new MemoryStream(Pdf), "application/pdf", CancellationToken);
        var compensations = new Compensations(NullLoggerFactory.Instance);
        var handler = new DownloadDocumentHandler(
            new SingleInvoice(invoice), storage, compensations, new FixedUser(), new FixedTime());

        DocumentContent content = await handler.HandleAsync(new DownloadDocumentCommand(invoice.Id), CancellationToken);

        content.FileName.ShouldBe("FV-1.pdf");
        content.SizeBytes.ShouldBe(Pdf.Length);
        using (content.Content)
        {
            (await ReadAllAsync(content.Content)).ShouldBe(Pdf);
        }

        invoice.DomainEvents.ShouldHaveSingleItem().ShouldBeOfType<InvoiceDocumentDownloaded>();
    }

    [Fact]
    public async Task Handle_UnknownInvoice_ThrowsNotFound()
    {
        var handler = new DownloadDocumentHandler(
            new SingleInvoice(null),
            new InMemoryDocumentStorage(),
            new Compensations(NullLoggerFactory.Instance),
            new FixedUser(),
            new FixedTime());

        await Should.ThrowAsync<InvoiceNotFoundException>(() =>
            handler.HandleAsync(new DownloadDocumentCommand(new InvoiceId(Guid.CreateVersion7())), CancellationToken));
    }

    private static async Task<byte[]> ReadAllAsync(Stream stream)
    {
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, CancellationToken);
        return buffer.ToArray();
    }

    private sealed class SingleInvoice(Invoice? invoice) : IInvoiceRepository
    {
        public Task<Invoice?> GetAsync(InvoiceId id, CancellationToken cancellationToken) =>
            Task.FromResult(invoice is not null && invoice.Id == id ? invoice : null);

        public Task<Invoice?> GetAsync(InvoiceId id, string expectedVersion, CancellationToken cancellationToken) =>
            GetAsync(id, cancellationToken);

        public void Add(Invoice invoice) => throw new NotSupportedException();
    }

    private sealed class FixedUser : ICurrentUser
    {
        public Actor Actor => Clerk;
    }

    private sealed class FixedTime : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
