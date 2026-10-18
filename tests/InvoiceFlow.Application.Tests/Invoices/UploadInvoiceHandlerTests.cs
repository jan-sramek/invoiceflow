using InvoiceFlow.Application.Abstractions.Context;
using InvoiceFlow.Application.Abstractions.Persistence;
using InvoiceFlow.Application.Behaviors;
using InvoiceFlow.Application.Errors;
using InvoiceFlow.Application.Invoices.Upload;
using InvoiceFlow.Domain.Common;
using InvoiceFlow.Domain.Invoices;
using InvoiceFlow.Domain.Tenants;
using InvoiceFlow.Testing.Storage;
using Microsoft.Extensions.Logging.Abstractions;

namespace InvoiceFlow.Application.Tests.Invoices;

public class UploadInvoiceHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 15, 12, 0, 0, TimeSpan.Zero);
    private static readonly Actor Clerk = Actor.User("clerk-1", "Clara Clerk");
    private static readonly TenantId Tenant = new(Guid.Parse("0199a0a0-0000-7000-8000-000000000001"));
    private static readonly byte[] Pdf = "%PDF-1.4\n%InvoiceFlow test\n"u8.ToArray();

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Handle_Pdf_StoresDocumentAndAddsInvoice()
    {
        var storage = new InMemoryDocumentStorage();
        var invoices = new CapturingRepository();
        UploadInvoiceHandler handler = Handler(storage, invoices);

        await using var file = new MemoryStream(Pdf);
        InvoiceId id = await handler.HandleAsync(new UploadInvoiceCommand(file, "path/FV-1.pdf"), CancellationToken);

        Invoice invoice = invoices.Added.ShouldNotBeNull();
        invoice.Id.ShouldBe(id);
        invoice.Status.ShouldBe(InvoiceStatus.Uploaded);
        invoice.Document.FileName.ShouldBe("FV-1.pdf");
        invoice.Document.SizeBytes.ShouldBe(Pdf.Length);
        invoice.Document.StorageKey.ShouldBe($"tenants/{Tenant.Value:D}/documents/{invoice.Document.Id:D}");
        storage.Documents.Keys.ShouldBe([invoice.Document.StorageKey]);
        storage.Documents[invoice.Document.StorageKey].Content.ShouldBe(Pdf);
        storage.Documents[invoice.Document.StorageKey].ContentType.ShouldBe(UploadInvoiceHandler.PdfContentType);
    }

    [Fact]
    public async Task Handle_TooLarge_RejectsWithoutStoring()
    {
        var storage = new InMemoryDocumentStorage();
        var invoices = new CapturingRepository();
        byte[] large = new byte[UploadInvoiceCommand.MaxBytes + 1];
        "%PDF-"u8.CopyTo(large);

        UploadTooLargeException exception = await Should.ThrowAsync<UploadTooLargeException>(() =>
            Handler(storage, invoices).HandleAsync(new UploadInvoiceCommand(new MemoryStream(large), "big.pdf"), CancellationToken));

        exception.Params["maxBytes"].ShouldBe(UploadInvoiceCommand.MaxBytes);
        storage.Documents.ShouldBeEmpty();
        invoices.Added.ShouldBeNull();
    }

    [Fact]
    public async Task Handle_NotPdf_RejectsWithoutStoring()
    {
        var storage = new InMemoryDocumentStorage();
        var invoices = new CapturingRepository();

        await Should.ThrowAsync<UploadNotPdfException>(() =>
            Handler(storage, invoices).HandleAsync(
                new UploadInvoiceCommand(new MemoryStream("not a pdf"u8.ToArray()), "invoice.pdf"), CancellationToken));

        storage.Documents.ShouldBeEmpty();
        invoices.Added.ShouldBeNull();
    }

    [Fact]
    public async Task Handle_StorageUnavailable_DoesNotAddInvoice()
    {
        var storage = new InMemoryDocumentStorage { Unavailable = true };
        var invoices = new CapturingRepository();

        await Should.ThrowAsync<StorageUnavailableException>(() =>
            Handler(storage, invoices).HandleAsync(new UploadInvoiceCommand(new MemoryStream(Pdf), "a.pdf"), CancellationToken));

        invoices.Added.ShouldBeNull();
    }

    private static UploadInvoiceHandler Handler(InMemoryDocumentStorage storage, CapturingRepository invoices) =>
        new(
            invoices,
            storage,
            new Compensations(NullLoggerFactory.Instance),
            new FixedTenant(),
            new FixedUser(),
            new FixedTime());

    private sealed class CapturingRepository : IInvoiceRepository
    {
        public Invoice? Added { get; private set; }

        public Task<Invoice?> GetAsync(InvoiceId id, CancellationToken cancellationToken) =>
            Task.FromResult<Invoice?>(null);

        public Task<Invoice?> GetAsync(InvoiceId id, string expectedVersion, CancellationToken cancellationToken) =>
            Task.FromResult<Invoice?>(null);

        public void Add(Invoice invoice) => Added = invoice;
    }

    private sealed class FixedTenant : ITenantContext
    {
        public TenantId TenantId => Tenant;
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
