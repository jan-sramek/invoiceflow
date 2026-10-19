using System.Security.Cryptography;
using InvoiceFlow.Application.Abstractions.Persistence;
using InvoiceFlow.Domain.Common;
using InvoiceFlow.Domain.Invoices;
using InvoiceFlow.Infrastructure.Documents;
using InvoiceFlow.Infrastructure.Extraction;
using Microsoft.Extensions.Logging.Abstractions;

namespace InvoiceFlow.Worker.Tests;

public class InvoiceProcessingServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 19, 12, 0, 0, TimeSpan.Zero);
    private static readonly Actor Clerk = Actor.User("clerk-1", "Clara Clerk");

    [Fact]
    public async Task ProcessAsync_StubExtractor_MovesInvoiceToNeedsReview()
    {
        var documentId = Guid.CreateVersion7(Now);
        byte[] pdf = "%PDF-1.4 stub"u8.ToArray();
        string sha256 = Convert.ToHexString(SHA256.HashData(pdf)).ToLowerInvariant();
        var document = InvoiceDocument.Create(documentId, "tenant/doc", "invoice.pdf", pdf.Length, sha256);
        var invoice = Invoice.Upload(Clerk, Now, document);

        var storage = new InMemoryDocumentStorage();
        await using (var content = new MemoryStream(pdf, writable: false))
        {
            await storage.PutAsync(
                document.StorageKey, content, InvoiceDocument.PdfContentType, pdf.Length, TestContext.Current.CancellationToken);
        }

        var repository = new FakeInvoiceRepository(invoice);
        var unitOfWork = new FakeUnitOfWork();
        var service = new InvoiceProcessingService(
            repository,
            storage,
            new StubInvoiceExtractor(),
            unitOfWork,
            TimeProvider.System,
            NullLogger<InvoiceProcessingService>.Instance);

        await service.ProcessAsync(invoice.Id, TestContext.Current.CancellationToken);

        invoice.Status.ShouldBe(InvoiceStatus.NeedsReview);
        unitOfWork.Committed.ShouldBeTrue();
    }

    [Fact]
    public async Task ProcessAsync_StorageFailure_FailsExtraction()
    {
        var documentId = Guid.CreateVersion7(Now);
        byte[] pdf = "%PDF-1.4 stub"u8.ToArray();
        string sha256 = Convert.ToHexString(SHA256.HashData(pdf)).ToLowerInvariant();
        var document = InvoiceDocument.Create(documentId, "missing-key", "invoice.pdf", pdf.Length, sha256);
        var invoice = Invoice.Upload(Clerk, Now, document);

        var service = new InvoiceProcessingService(
            new FakeInvoiceRepository(invoice),
            new InMemoryDocumentStorage(),
            new StubInvoiceExtractor(),
            new FakeUnitOfWork(),
            TimeProvider.System,
            NullLogger<InvoiceProcessingService>.Instance);

        await service.ProcessAsync(invoice.Id, TestContext.Current.CancellationToken);

        invoice.Status.ShouldBe(InvoiceStatus.ExtractionFailed);
    }

    private sealed class FakeInvoiceRepository(Invoice invoice) : IInvoiceRepository
    {
        public Task<Invoice?> GetAsync(InvoiceId id, CancellationToken cancellationToken) =>
            Task.FromResult<Invoice?>(id.Value == invoice.Id.Value ? invoice : null);

        public Task<Invoice?> GetAsync(InvoiceId id, string expectedVersion, CancellationToken cancellationToken) =>
            GetAsync(id, cancellationToken);

        public Task<InvoiceId?> FindIdByDocumentSha256Async(string sha256, CancellationToken cancellationToken) =>
            Task.FromResult<InvoiceId?>(null);

        public void Add(Invoice newInvoice) => throw new NotSupportedException();
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public bool Committed { get; private set; }

        public void AfterCommit(Func<CancellationToken, Task> callback)
        {
        }

        public Task CommitAsync(CancellationToken cancellationToken)
        {
            Committed = true;
            return Task.CompletedTask;
        }
    }
}
