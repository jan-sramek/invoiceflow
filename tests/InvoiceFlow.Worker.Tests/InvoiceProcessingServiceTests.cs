using System.Security.Cryptography;
using InvoiceFlow.Application.Abstractions.Extraction;
using InvoiceFlow.Application.Abstractions.Persistence;
using InvoiceFlow.Application.Errors;
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
    private static readonly Guid MessageId = Guid.Parse("11111111-1111-1111-1111-111111111111");

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
        var processed = new FakeProcessedMessageStore();
        var service = new InvoiceProcessingService(
            repository,
            storage,
            new StubInvoiceExtractor(),
            processed,
            unitOfWork,
            TimeProvider.System,
            NullLogger<InvoiceProcessingService>.Instance);

        await service.ProcessAsync(MessageId, invoice.Id, TestContext.Current.CancellationToken);

        invoice.Status.ShouldBe(InvoiceStatus.NeedsReview);
        unitOfWork.Committed.ShouldBeTrue();
        processed.Added.ShouldContain(MessageId);
    }

    [Fact]
    public async Task ProcessAsync_BusinessExtractionFailure_FailsWithoutThrowing()
    {
        var documentId = Guid.CreateVersion7(Now);
        byte[] pdf = MinimalPdf.EmptyPage();
        string sha256 = Convert.ToHexString(SHA256.HashData(pdf)).ToLowerInvariant();
        var document = InvoiceDocument.Create(documentId, "tenant/doc", "invoice.pdf", pdf.Length, sha256);
        var invoice = Invoice.Upload(Clerk, Now, document);

        var storage = new InMemoryDocumentStorage();
        await using (var content = new MemoryStream(pdf, writable: false))
        {
            await storage.PutAsync(
                document.StorageKey, content, InvoiceDocument.PdfContentType, pdf.Length, TestContext.Current.CancellationToken);
        }

        var processed = new FakeProcessedMessageStore();
        var service = new InvoiceProcessingService(
            new FakeInvoiceRepository(invoice),
            storage,
            new FixedExtractor(new ExtractionResult.Failed(ApplicationErrors.Extraction.NoTextLayer)),
            processed,
            new FakeUnitOfWork(),
            TimeProvider.System,
            NullLogger<InvoiceProcessingService>.Instance);

        await service.ProcessAsync(MessageId, invoice.Id, TestContext.Current.CancellationToken);

        invoice.Status.ShouldBe(InvoiceStatus.ExtractionFailed);
        processed.Added.ShouldContain(MessageId);
    }

    [Fact]
    public async Task ProcessAsync_StorageFailure_RethrowsTechnicalError()
    {
        var documentId = Guid.CreateVersion7(Now);
        byte[] pdf = "%PDF-1.4 stub"u8.ToArray();
        string sha256 = Convert.ToHexString(SHA256.HashData(pdf)).ToLowerInvariant();
        var document = InvoiceDocument.Create(documentId, "missing-key", "invoice.pdf", pdf.Length, sha256);
        var invoice = Invoice.Upload(Clerk, Now, document);
        var unitOfWork = new FakeUnitOfWork();
        var processed = new FakeProcessedMessageStore();

        var service = new InvoiceProcessingService(
            new FakeInvoiceRepository(invoice),
            new InMemoryDocumentStorage(),
            new StubInvoiceExtractor(),
            processed,
            unitOfWork,
            TimeProvider.System,
            NullLogger<InvoiceProcessingService>.Instance);

        await Should.ThrowAsync<Exception>(() =>
            service.ProcessAsync(MessageId, invoice.Id, TestContext.Current.CancellationToken));

        invoice.Status.ShouldNotBe(InvoiceStatus.ExtractionFailed);
        unitOfWork.Committed.ShouldBeFalse();
        processed.Added.ShouldBeEmpty();
    }

    [Fact]
    public async Task ProcessAsync_DuplicateMessage_SkipsWithoutReextracting()
    {
        var documentId = Guid.CreateVersion7(Now);
        byte[] pdf = "%PDF-1.4 stub"u8.ToArray();
        string sha256 = Convert.ToHexString(SHA256.HashData(pdf)).ToLowerInvariant();
        var document = InvoiceDocument.Create(documentId, "tenant/doc", "invoice.pdf", pdf.Length, sha256);
        var invoice = Invoice.Upload(Clerk, Now, document);
        invoice.StartProcessing(Actor.System("worker"), Now);
        invoice.RequireReview(Actor.System("worker"), Now);

        var extractor = new CountingExtractor();
        var processed = new FakeProcessedMessageStore();
        processed.Add(MessageId, Now);

        var service = new InvoiceProcessingService(
            new FakeInvoiceRepository(invoice),
            new InMemoryDocumentStorage(),
            extractor,
            processed,
            new FakeUnitOfWork(),
            TimeProvider.System,
            NullLogger<InvoiceProcessingService>.Instance);

        await service.ProcessAsync(MessageId, invoice.Id, TestContext.Current.CancellationToken);

        extractor.Calls.ShouldBe(0);
        invoice.Status.ShouldBe(InvoiceStatus.NeedsReview);
    }

    private sealed class FixedExtractor(ExtractionResult result) : IInvoiceExtractor
    {
        public Task<ExtractionResult> ExtractAsync(Stream pdf, CancellationToken cancellationToken) =>
            Task.FromResult(result);
    }

    private sealed class CountingExtractor : IInvoiceExtractor
    {
        public int Calls { get; private set; }

        public Task<ExtractionResult> ExtractAsync(Stream pdf, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult<ExtractionResult>(new ExtractionResult.Succeeded(InvoiceFields.Empty));
        }
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

    private sealed class FakeProcessedMessageStore : IProcessedMessageStore
    {
        private readonly HashSet<Guid> _ids = [];

        public IReadOnlyCollection<Guid> Added => _ids;

        public Task<bool> ExistsAsync(Guid messageId, CancellationToken cancellationToken) =>
            Task.FromResult(_ids.Contains(messageId));

        public void Add(Guid messageId, DateTimeOffset processedAt) => _ids.Add(messageId);
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
