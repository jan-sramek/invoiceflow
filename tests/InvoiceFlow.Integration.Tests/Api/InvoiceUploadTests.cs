using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using InvoiceFlow.Application.Errors;
using InvoiceFlow.Domain.Common;
using InvoiceFlow.Domain.Invoices;
using InvoiceFlow.Integration.Tests.Persistence;

namespace InvoiceFlow.Integration.Tests.Api;

public sealed class InvoiceUploadTests(PostgresFixture database) : InvoiceApiTests(database)
{
    private static readonly DateTimeOffset Start = new(2026, 10, 15, 9, 0, 0, TimeSpan.Zero);
    private static readonly Actor Clerk = Actor.User("clerk-1", "Clara Clerk");
    private static readonly byte[] Pdf = "%PDF-1.4\n%InvoiceFlow upload test\n"u8.ToArray();

    [Fact]
    public async Task Upload_Pdf_CreatesInvoiceAndStoresDocument()
    {
        HttpResponseMessage response = await UploadAsync(Pdf, "folder/FV-2026-0117.pdf");
        JsonElement body = await JsonAsync(response);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        response.Headers.Location.ShouldNotBeNull().AbsolutePath
            .ShouldBe($"/api/v1/invoices/{body.GetProperty("id").GetGuid()}");
        response.Headers.ETag.ShouldNotBeNull().IsWeak.ShouldBeFalse();
        body.GetProperty("status").GetString().ShouldBe("Uploaded");
        body.GetProperty("document").GetProperty("fileName").GetString().ShouldBe("FV-2026-0117.pdf");
        body.GetProperty("document").GetProperty("sizeBytes").GetInt64().ShouldBe(Pdf.Length);
        body.GetProperty("uploadedBy").GetProperty("id").GetString().ShouldBe("dev-clerk");

        string key = Storage.Documents.Keys.ShouldHaveSingleItem();
        key.ShouldStartWith($"tenants/{Tenant.Id.Value:D}/documents/");
        Storage.Documents[key].Content.ShouldBe(Pdf);
    }

    [Fact]
    public async Task Upload_WithoutUser_IsUnauthorized()
    {
        HttpResponseMessage response = await UploadAsync(Pdf, "a.pdf", persona: null);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await JsonAsync(response)).GetProperty("code").GetString().ShouldBe(ApplicationErrors.Auth.UserRequired);
        Storage.Documents.ShouldBeEmpty();
    }

    [Fact]
    public async Task Upload_NotPdf_IsUnsupportedMediaType()
    {
        HttpResponseMessage response = await UploadAsync("not a pdf"u8.ToArray(), "invoice.pdf");

        response.StatusCode.ShouldBe(HttpStatusCode.UnsupportedMediaType);
        (await JsonAsync(response)).GetProperty("code").GetString().ShouldBe(ApplicationErrors.Upload.NotPdf);
        Storage.Documents.ShouldBeEmpty();
    }

    [Fact]
    public async Task Upload_TooLarge_IsPayloadTooLarge()
    {
        byte[] large = new byte[10 * 1024 * 1024 + 1];
        "%PDF-"u8.CopyTo(large);

        HttpResponseMessage response = await UploadAsync(large, "big.pdf");
        JsonElement body = await JsonAsync(response);

        response.StatusCode.ShouldBe(HttpStatusCode.RequestEntityTooLarge);
        body.GetProperty("code").GetString().ShouldBe(ApplicationErrors.Upload.TooLarge);
        body.GetProperty("params").GetProperty("maxBytes").GetInt64().ShouldBe(10L * 1024 * 1024);
        Storage.Documents.ShouldBeEmpty();
    }

    [Fact]
    public async Task Upload_StorageUnavailable_IsServiceUnavailable()
    {
        Storage.Unavailable = true;

        HttpResponseMessage response = await UploadAsync(Pdf, "a.pdf");

        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        (await JsonAsync(response)).GetProperty("code").GetString().ShouldBe(ApplicationErrors.Storage.Unavailable);
        response.Headers.RetryAfter.ShouldNotBeNull();
    }

    [Fact]
    public async Task Download_ReturnsPdfAsAttachment()
    {
        InvoiceDocument document = TestDocuments.New("Downloaded.pdf");
        var invoice = Invoice.Upload(Clerk, Start, document);
        await Storage.StoreAsync(document.StorageKey, new MemoryStream(Pdf), "application/pdf", CancellationToken);
        await AddAsync(Tenant.Id, invoice);

        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/invoices/{invoice.Id}/document");
        request.Headers.Add("X-Development-User", "approver");
        HttpResponseMessage response = await Client.SendAsync(request, CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType.ShouldNotBeNull().MediaType.ShouldBe("application/pdf");
        ContentDispositionHeaderValue disposition = response.Content.Headers.ContentDisposition.ShouldNotBeNull();
        (disposition.FileName ?? disposition.FileNameStar).ShouldNotBeNull().ShouldContain("Downloaded.pdf");
        response.Headers.GetValues("X-Content-Type-Options").ShouldBe(["nosniff"]);
        (await response.Content.ReadAsByteArrayAsync(CancellationToken)).ShouldBe(Pdf);
    }

    [Fact]
    public async Task Download_WithoutUser_IsUnauthorized()
    {
        (InvoiceId id, _) = await SeedAsync(Invoice.Upload(Clerk, Start, TestDocuments.New()));

        HttpResponseMessage response = await Client.GetAsync($"/api/v1/invoices/{id}/document", CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Download_UnknownInvoice_IsNotFound()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/invoices/{Guid.CreateVersion7()}/document");
        request.Headers.Add("X-Development-User", "clerk");

        HttpResponseMessage response = await Client.SendAsync(request, CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await JsonAsync(response)).GetProperty("code").GetString().ShouldBe(ApplicationErrors.Invoice.NotFound);
    }

    private async Task<HttpResponseMessage> UploadAsync(byte[] content, string fileName, string? persona = "clerk")
    {
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(content);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(file, "file", fileName);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/invoices") { Content = form };
        if (persona is not null)
        {
            request.Headers.Add("X-Development-User", persona);
        }

        return await Client.SendAsync(request, CancellationToken);
    }
}
