using System.Net;
using System.Text.Json;
using InvoiceFlow.Application.Abstractions.Persistence;
using InvoiceFlow.Domain.Common;
using InvoiceFlow.Domain.Invoices;
using InvoiceFlow.Domain.Tenants;
using InvoiceFlow.Integration.Tests.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace InvoiceFlow.Integration.Tests.Api;

public sealed class InvoiceDetailTests(PostgresFixture database) : IAsyncLifetime
{
    private static readonly DateTimeOffset Start = new(2026, 10, 12, 9, 0, 0, TimeSpan.Zero);
    private static readonly Actor Clerk = Actor.User("clerk-1", "Clara Clerk");
    private static readonly Actor Finance = Actor.User("finance-1", "Fiona Finance");

    private TestTenant _tenant = null!;
    private TenantApi _api = null!;
    private HttpClient _client = null!;

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        _tenant = await database.CreateTenantAsync();
        _api = new TenantApi(database, _tenant.Id);
        _client = _api.CreateClient();
    }

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await _api.DisposeAsync();
    }

    [Fact]
    public async Task Get_PaidInvoice_ReturnsEveryValue()
    {
        Invoice invoice = PaidInvoice();
        await AddAsync(_tenant.Id, invoice);

        HttpResponseMessage response = await GetAsync(invoice.Id.Value);
        JsonElement body = await JsonAsync(response);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        body.GetProperty("id").GetGuid().ShouldBe(invoice.Id.Value);
        body.GetProperty("status").GetString().ShouldBe("Paid");
        body.GetProperty("uploadedBy").GetProperty("name").GetString().ShouldBe("Clara Clerk");
        body.GetProperty("uploadedBy").GetProperty("type").GetString().ShouldBe("User");
        body.GetProperty("uploadedAt").GetDateTimeOffset().ShouldBe(Start);
        body.GetProperty("submittedBy").GetProperty("id").GetString().ShouldBe("clerk-1");
        body.GetProperty("paidOn").GetString().ShouldBe("2026-10-12");
        body.GetProperty("paymentReference").GetString().ShouldBe("BANK-42");

        JsonElement fields = body.GetProperty("fields");
        fields.GetProperty("supplierName").GetString().ShouldBe("Acme s.r.o.");
        fields.GetProperty("invoiceNumber").GetString().ShouldBe("FV-2026-0117");
        fields.GetProperty("invoiceDate").GetString().ShouldBe("2026-10-01");
        fields.GetProperty("dueDate").GetString().ShouldBe("2026-10-31");
        fields.GetProperty("currency").GetString().ShouldBe("CZK");
        fields.GetProperty("subtotal").GetDecimal().ShouldBe(1000.00m);
        fields.GetProperty("taxAmount").GetDecimal().ShouldBe(210.50m);
        fields.GetProperty("totalAmount").GetRawText().ShouldBe("1210.50");
        fields.GetProperty("purchaseOrderReference").GetString().ShouldBe("PO-77");
    }

    [Fact]
    public async Task Get_NewInvoice_ReturnsMissingValuesAsNull()
    {
        var invoice = Invoice.Upload(Clerk, Start);
        await AddAsync(_tenant.Id, invoice);

        JsonElement body = await JsonAsync(await GetAsync(invoice.Id.Value));

        body.GetProperty("status").GetString().ShouldBe("Uploaded");
        body.GetProperty("submittedBy").ValueKind.ShouldBe(JsonValueKind.Null);
        body.GetProperty("paidOn").ValueKind.ShouldBe(JsonValueKind.Null);
        body.GetProperty("fields").EnumerateObject().ShouldAllBe(field => field.Value.ValueKind == JsonValueKind.Null);
    }

    [Fact]
    [Trait("Requirement", "FR-DET-03")]
    public async Task Get_ReturnsStrongETagThatChangesWithTheInvoice()
    {
        var invoice = Invoice.Upload(Clerk, Start);
        await AddAsync(_tenant.Id, invoice);

        HttpResponseMessage before = await GetAsync(invoice.Id.Value);
        HttpResponseMessage again = await GetAsync(invoice.Id.Value);
        await ChangeAsync(invoice.Id, i => i.Cancel(Clerk, Start.AddMinutes(1), "Sent twice"));
        HttpResponseMessage after = await GetAsync(invoice.Id.Value);

        before.Headers.ETag.ShouldNotBeNull().IsWeak.ShouldBeFalse();
        again.Headers.ETag.ShouldBe(before.Headers.ETag);
        after.Headers.ETag.ShouldNotBe(before.Headers.ETag);
    }

    [Fact]
    public async Task Get_IsNeverCached()
    {
        var invoice = Invoice.Upload(Clerk, Start);
        await AddAsync(_tenant.Id, invoice);

        HttpResponseMessage response = await GetAsync(invoice.Id.Value);

        response.Headers.CacheControl.ShouldNotBeNull().NoStore.ShouldBeTrue();
    }

    [Fact]
    public async Task Get_UnknownId_ReturnsInvoiceNotFound()
    {
        HttpResponseMessage response = await GetAsync(Guid.CreateVersion7());
        JsonElement problem = await JsonAsync(response);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        problem.GetProperty("code").GetString().ShouldBe("invoice.not_found");
    }

    [Fact]
    [Trait("Requirement", "NFR-SEC-03")]
    public async Task Get_InvoiceOfAnotherTenant_LooksExactlyLikeUnknownId()
    {
        TestTenant other = await database.CreateTenantAsync();
        var foreign = Invoice.Upload(Clerk, Start);
        await AddAsync(other.Id, foreign);

        JsonElement foreignProblem = await JsonAsync(await GetAsync(foreign.Id.Value));
        JsonElement unknownProblem = await JsonAsync(await GetAsync(Guid.CreateVersion7()));

        Comparable(foreignProblem).ShouldBe(Comparable(unknownProblem));
    }

    [Fact]
    public async Task Get_IdThatIsNotAGuid_ReturnsRequestNotFound()
    {
        HttpResponseMessage response = await _client.GetAsync("/api/v1/invoices/123", CancellationToken);
        JsonElement problem = await JsonAsync(response);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        problem.GetProperty("code").GetString().ShouldBe("request.not_found");
    }

    private static Invoice PaidInvoice()
    {
        var invoice = Invoice.Upload(Clerk, Start);
        invoice.EnterManually(Clerk, Start.AddMinutes(1));
        invoice.UpdateFields(Clerk, Start.AddMinutes(2), new InvoiceFields
        {
            SupplierName = SupplierName.Create("Acme s.r.o."),
            InvoiceNumber = InvoiceNumber.Create("FV-2026-0117"),
            InvoiceDate = new DateOnly(2026, 10, 1),
            DueDate = new DateOnly(2026, 10, 31),
            Currency = CurrencyCode.Create("CZK"),
            Subtotal = 1000.00m,
            TaxAmount = 210.50m,
            TotalAmount = 1210.50m,
            PurchaseOrderReference = PurchaseOrderReference.Create("PO-77"),
        });
        invoice.Submit(Clerk, Start.AddMinutes(3));
        invoice.Approve(Finance, Start.AddMinutes(4));
        invoice.MarkPaid(Finance, Start.AddMinutes(5), new DateOnly(2026, 10, 12), "BANK-42");
        return invoice;
    }

    // Everything except the values that differ per request
    private static string Comparable(JsonElement problem) => string.Join(
        '\n',
        problem.EnumerateObject()
            .Where(property => property.Name is not ("traceId" or "instance"))
            .Select(property => $"{property.Name}={property.Value.GetRawText()}"));

    private Task<HttpResponseMessage> GetAsync(Guid id) =>
        _client.GetAsync($"/api/v1/invoices/{id}", CancellationToken);

    private static async Task<JsonElement> JsonAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(CancellationToken));
        return document.RootElement.Clone();
    }

    private async Task AddAsync(TenantId tenant, Invoice invoice)
    {
        await using AsyncServiceScope scope = database.OpenScope(tenant);
        scope.ServiceProvider.GetRequiredService<IInvoiceRepository>().Add(invoice);
        await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().CommitAsync(CancellationToken);
    }

    private async Task ChangeAsync(InvoiceId id, Action<Invoice> change)
    {
        await using AsyncServiceScope scope = database.OpenScope(_tenant.Id);
        Invoice invoice = (await scope.ServiceProvider.GetRequiredService<IInvoiceRepository>().GetAsync(id, CancellationToken))
            .ShouldNotBeNull();
        change(invoice);
        await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().CommitAsync(CancellationToken);
    }
}
