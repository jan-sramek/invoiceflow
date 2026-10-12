using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using InvoiceFlow.Application.Abstractions.Persistence;
using InvoiceFlow.Domain.Common;
using InvoiceFlow.Domain.Invoices;
using InvoiceFlow.Domain.Tenants;
using InvoiceFlow.Integration.Tests.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace InvoiceFlow.Integration.Tests.Api;

public sealed class InvoiceWriteTests(PostgresFixture database) : IAsyncLifetime
{
    private static readonly DateTimeOffset Start = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);
    private static readonly Actor Uploader = Actor.User("clerk-1", "Clara Clerk");

    private static readonly object CompleteFields = new
    {
        supplierName = "Acme s.r.o.",
        invoiceNumber = "FV-2026-0117",
        invoiceDate = "2026-10-01",
        dueDate = "2026-10-31",
        currency = "CZK",
        subtotal = 1000.00m,
        taxAmount = 210.00m,
        totalAmount = 1210.00m,
        purchaseOrderReference = (string?)null,
    };

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
    [Trait("Requirement", "FR-EXT-04")]
    public async Task UpdateFields_ReturnsChangedInvoiceWithNewETag()
    {
        (InvoiceId id, EntityTagHeaderValue etag) = await InvoiceInReviewAsync();

        HttpResponseMessage response = await UpdateFieldsAsync(id, etag, CompleteFields);
        JsonElement body = await JsonAsync(response);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Headers.ETag.ShouldNotBeNull().ShouldNotBe(etag);
        body.GetProperty("fields").GetProperty("supplierName").GetString().ShouldBe("Acme s.r.o.");
        body.GetProperty("fields").GetProperty("totalAmount").GetDecimal().ShouldBe(1210.00m);
        response.Headers.ETag.ShouldBe((await GetAsync(id)).Headers.ETag);
    }

    [Fact]
    public async Task UpdateFields_BlankValues_ClearFields()
    {
        (InvoiceId id, EntityTagHeaderValue etag) = await InvoiceInReviewAsync();
        HttpResponseMessage filled = await UpdateFieldsAsync(id, etag, CompleteFields);

        JsonElement body = await JsonAsync(await UpdateFieldsAsync(id, filled.Headers.ETag!, new { supplierName = " " }));

        body.GetProperty("fields").EnumerateObject().ShouldAllBe(field => field.Value.ValueKind == JsonValueKind.Null);
    }

    [Fact]
    public async Task UpdateFields_InvalidValues_ReportsEveryField()
    {
        (InvoiceId id, EntityTagHeaderValue etag) = await InvoiceInReviewAsync();

        HttpResponseMessage response = await UpdateFieldsAsync(
            id, etag, new { supplierName = new string('a', 201), currency = "XYZ", subtotal = 1.234m });
        JsonElement problem = await JsonAsync(response);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        problem.GetProperty("code").GetString().ShouldBe("validation.failed");
        problem.GetProperty("errors").EnumerateArray()
            .Select(e => (e.GetProperty("pointer").GetString(), e.GetProperty("code").GetString()))
            .ShouldBe(
            [
                ("/supplierName", "validation.max_length"),
                ("/currency", "validation.currency_invalid"),
                ("/subtotal", "validation.amount_precision"),
            ]);
        (await GetAsync(id)).Headers.ETag.ShouldBe(etag);
    }

    [Fact]
    public async Task UpdateFields_InStatusThatForbidsEdits_ReturnsInvalidTransition()
    {
        (InvoiceId id, EntityTagHeaderValue etag) = await SeedAsync(Invoice.Upload(Uploader, Start));

        JsonElement problem = await JsonAsync(await UpdateFieldsAsync(id, etag, CompleteFields));

        problem.GetProperty("status").GetInt32().ShouldBe(409);
        problem.GetProperty("code").GetString().ShouldBe("invoice.invalid_transition");
    }

    [Fact]
    [Trait("Requirement", "FR-EXT-06")]
    public async Task EnterManually_MovesUploadedInvoiceToReview()
    {
        (InvoiceId id, EntityTagHeaderValue etag) = await SeedAsync(Invoice.Upload(Uploader, Start));

        HttpResponseMessage response = await PostAsync(id, "enter-manually", etag);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await JsonAsync(response)).GetProperty("status").GetString().ShouldBe("NeedsReview");
    }

    [Fact]
    [Trait("Requirement", "FR-APR-01")]
    public async Task Submit_CompleteInvoice_RecordsSubmitter()
    {
        (InvoiceId id, EntityTagHeaderValue etag) = await InvoiceInReviewAsync();
        HttpResponseMessage updated = await UpdateFieldsAsync(id, etag, CompleteFields);

        HttpResponseMessage response = await PostAsync(id, "submit", updated.Headers.ETag!);
        JsonElement body = await JsonAsync(response);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        body.GetProperty("status").GetString().ShouldBe("PendingApproval");
        body.GetProperty("submittedBy").GetProperty("id").GetString().ShouldBe("dev-clerk");
        body.GetProperty("submittedBy").GetProperty("name").GetString().ShouldBe("Clara Clerk");
    }

    [Fact]
    [Trait("Requirement", "FR-APR-01")]
    public async Task Submit_IncompleteInvoice_ListsBlockingFields()
    {
        (InvoiceId id, EntityTagHeaderValue etag) = await InvoiceInReviewAsync();
        HttpResponseMessage updated = await UpdateFieldsAsync(id, etag, new { supplierName = "Acme s.r.o.", subtotal = 1000m, taxAmount = 210m, totalAmount = 1000m });

        HttpResponseMessage response = await PostAsync(id, "submit", updated.Headers.ETag!);
        JsonElement problem = await JsonAsync(response);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        problem.GetProperty("code").GetString().ShouldBe("invoice.not_ready_for_submission");
        problem.GetProperty("errors").EnumerateArray()
            .Select(e => e.GetProperty("pointer").GetString())
            .ShouldBe(["/invoiceNumber", "/invoiceDate", "/currency", "/totalAmount"], ignoreOrder: true);
    }

    [Fact]
    public async Task Write_WithoutIfMatch_ReturnsPreconditionRequired()
    {
        (InvoiceId id, _) = await SeedAsync(Invoice.Upload(Uploader, Start));
        using HttpRequestMessage request = Request(HttpMethod.Post, $"/api/v1/invoices/{id}/enter-manually", etag: null);

        HttpResponseMessage response = await _client.SendAsync(request, CancellationToken);
        JsonElement problem = await JsonAsync(response);

        response.StatusCode.ShouldBe((HttpStatusCode)428);
        problem.GetProperty("code").GetString().ShouldBe("concurrency.if_match_required");
    }

    [Fact]
    [Trait("Requirement", "FR-DET-03")]
    public async Task Write_WithStaleETag_ChangesNothing()
    {
        (InvoiceId id, EntityTagHeaderValue stale) = await InvoiceInReviewAsync();
        HttpResponseMessage first = await UpdateFieldsAsync(id, stale, CompleteFields);

        HttpResponseMessage second = await UpdateFieldsAsync(id, stale, new { supplierName = "Overwritten" });
        JsonElement problem = await JsonAsync(second);

        second.StatusCode.ShouldBe(HttpStatusCode.PreconditionFailed);
        problem.GetProperty("code").GetString().ShouldBe("concurrency.stale_version");
        JsonElement current = await JsonAsync(await GetAsync(id));
        current.GetProperty("fields").GetProperty("supplierName").GetString().ShouldBe("Acme s.r.o.");
        (await GetAsync(id)).Headers.ETag.ShouldBe(first.Headers.ETag);
    }

    [Fact]
    [Trait("Requirement", "FR-DET-03")]
    public async Task Write_ConcurrentWithSameETag_OnlyOneWins()
    {
        (InvoiceId id, EntityTagHeaderValue etag) = await InvoiceInReviewAsync();

        HttpResponseMessage[] responses = await Task.WhenAll(
            UpdateFieldsAsync(id, etag, new { supplierName = "First s.r.o." }),
            UpdateFieldsAsync(id, etag, new { supplierName = "Second s.r.o." }),
            UpdateFieldsAsync(id, etag, new { supplierName = "Third s.r.o." }));

        responses.Count(r => r.StatusCode == HttpStatusCode.OK).ShouldBe(1);
        responses.Count(r => r.StatusCode == HttpStatusCode.PreconditionFailed).ShouldBe(2);
    }

    [Fact]
    public async Task Write_WithoutUser_ReturnsUserRequired()
    {
        (InvoiceId id, EntityTagHeaderValue etag) = await SeedAsync(Invoice.Upload(Uploader, Start));
        using HttpRequestMessage request = Request(HttpMethod.Post, $"/api/v1/invoices/{id}/enter-manually", etag, persona: null);

        HttpResponseMessage response = await _client.SendAsync(request, CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await JsonAsync(response)).GetProperty("code").GetString().ShouldBe("auth.user_required");
        (await JsonAsync(await GetAsync(id))).GetProperty("status").GetString().ShouldBe("Uploaded");
    }

    [Fact]
    [Trait("Requirement", "NFR-SEC-03")]
    public async Task Write_InvoiceOfAnotherTenant_ReturnsNotFound()
    {
        TestTenant other = await database.CreateTenantAsync();
        var foreign = Invoice.Upload(Uploader, Start);
        await AddAsync(other.Id, foreign);

        HttpResponseMessage response = await PostAsync(foreign.Id, "enter-manually", new EntityTagHeaderValue("\"1\""));

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await JsonAsync(response)).GetProperty("code").GetString().ShouldBe("invoice.not_found");
    }

    private async Task<(InvoiceId Id, EntityTagHeaderValue ETag)> InvoiceInReviewAsync()
    {
        var invoice = Invoice.Upload(Uploader, Start);
        invoice.EnterManually(Uploader, Start);
        return await SeedAsync(invoice);
    }

    private async Task<(InvoiceId Id, EntityTagHeaderValue ETag)> SeedAsync(Invoice invoice)
    {
        await AddAsync(_tenant.Id, invoice);
        return (invoice.Id, (await GetAsync(invoice.Id)).Headers.ETag.ShouldNotBeNull());
    }

    private async Task AddAsync(TenantId tenant, Invoice invoice)
    {
        await using AsyncServiceScope scope = database.OpenScope(tenant);
        scope.ServiceProvider.GetRequiredService<IInvoiceRepository>().Add(invoice);
        await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().CommitAsync(CancellationToken);
    }

    private Task<HttpResponseMessage> GetAsync(InvoiceId id) =>
        _client.GetAsync($"/api/v1/invoices/{id}", CancellationToken);

    private async Task<HttpResponseMessage> UpdateFieldsAsync(InvoiceId id, EntityTagHeaderValue etag, object fields)
    {
        using HttpRequestMessage request = Request(HttpMethod.Put, $"/api/v1/invoices/{id}/fields", etag);
        request.Content = JsonContent.Create(fields);
        return await _client.SendAsync(request, CancellationToken);
    }

    private async Task<HttpResponseMessage> PostAsync(InvoiceId id, string action, EntityTagHeaderValue etag)
    {
        using HttpRequestMessage request = Request(HttpMethod.Post, $"/api/v1/invoices/{id}/{action}", etag);
        return await _client.SendAsync(request, CancellationToken);
    }

    private static HttpRequestMessage Request(
        HttpMethod method, string path, EntityTagHeaderValue? etag, string? persona = "clerk")
    {
        var request = new HttpRequestMessage(method, path);
        if (etag is not null)
        {
            request.Headers.IfMatch.Add(etag);
        }

        if (persona is not null)
        {
            request.Headers.Add("X-Development-User", persona);
        }

        return request;
    }

    private static async Task<JsonElement> JsonAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(CancellationToken));
        return document.RootElement.Clone();
    }
}
