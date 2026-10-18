using InvoiceFlow.Application.Abstractions.Persistence;
using InvoiceFlow.Application.Common;
using InvoiceFlow.Application.Errors;
using InvoiceFlow.Application.Invoices.GetInvoice;
using InvoiceFlow.Application.Invoices.ListInvoices;
using InvoiceFlow.Domain.Common;
using InvoiceFlow.Domain.Invoices;

namespace InvoiceFlow.Application.Tests.Invoices;

public class GetInvoiceHandlerTests
{
    private static readonly InvoiceId Id = new(Guid.CreateVersion7());

    [Fact]
    public async Task Handle_KnownInvoice_ReturnsItWithVersion()
    {
        Versioned<InvoiceDetailDto> stored = new(Detail(), "42");
        var handler = new GetInvoiceHandler(new SingleInvoice(stored));

        Versioned<InvoiceDetailDto> result = await handler.HandleAsync(new GetInvoiceQuery(Id), TestContext.Current.CancellationToken);

        result.ShouldBeSameAs(stored);
    }

    [Fact]
    public async Task Handle_UnknownInvoice_ThrowsNotFound()
    {
        var handler = new GetInvoiceHandler(new SingleInvoice(new Versioned<InvoiceDetailDto>(Detail(), "42")));

        await Should.ThrowAsync<InvoiceNotFoundException>(() =>
            handler.HandleAsync(new GetInvoiceQuery(new InvoiceId(Guid.CreateVersion7())), TestContext.Current.CancellationToken));
    }

    private static InvoiceDetailDto Detail() => new(
        Id.Value,
        InvoiceStatus.Uploaded,
        new InvoiceFieldsDto(null, null, null, null, null, null, null, null, null),
        new ActorDto(ActorType.User, "clerk-1", "Clara Clerk"),
        DateTimeOffset.UnixEpoch,
        new DocumentDto("invoice.pdf", 1024),
        null,
        null,
        null);

    private sealed class SingleInvoice(Versioned<InvoiceDetailDto> invoice) : IInvoiceReadService
    {
        public Task<Versioned<InvoiceDetailDto>?> GetDetailAsync(InvoiceId id, CancellationToken cancellationToken) =>
            Task.FromResult(id.Value == invoice.Value.Id ? invoice : null);

        public Task<PagedList<InvoiceSummaryDto>> ListAsync(
            ListInvoicesQuery query, InvoiceSort sort, DateOnly today, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
