using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using InvoiceFlow.Domain.Invoices;
using InvoiceFlow.Integration.Tests.Persistence;

namespace InvoiceFlow.Integration.Tests.Api;

public sealed class InvoiceUploadTests(PostgresFixture database) : InvoiceApiTests(database)
{
    private static readonly byte[] PdfBytes = Encoding.ASCII.GetBytes("%PDF-1.4\n1 0 obj<<>>endobj\ntrailer<<>>\n%%EOF\n");

    [Fact]
    [Trait("Requirement", "FR-UPL-01")]
    public async Task Upload_Pdf_CreatesInvoiceInUploaded()
    {
        HttpResponseMessage response = await UploadAsync(PdfBytes, "acme.pdf", "key-1");
        JsonElement body = await JsonAsync(response);

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        response.Headers.ETag.ShouldNotBeNull();
        body.GetProperty("status").GetString().ShouldBe("Uploaded");
        body.GetProperty("document").GetProperty("originalFileName").GetString().ShouldBe("acme.pdf");
        body.GetProperty("duplicate").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Fact]
    [Trait("Requirement", "NFR-INT-05")]
    public async Task Upload_SameIdempotencyKey_ReturnsSameInvoice()
    {
        HttpResponseMessage first = await UploadAsync(PdfBytes, "a.pdf", "same-key");
        Guid id = (await JsonAsync(first)).GetProperty("id").GetGuid();

        HttpResponseMessage again = await UploadAsync(PdfBytes, "a.pdf", "same-key");

        again.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        (await JsonAsync(again)).GetProperty("id").GetGuid().ShouldBe(id);
    }

    [Fact]
    [Trait("Requirement", "FR-UPL-02")]
    public async Task Upload_NotPdf_Returns415()
    {
        HttpResponseMessage response = await UploadAsync("not a pdf"u8.ToArray(), "x.txt", "key-bad");
        JsonElement problem = await JsonAsync(response);

        response.StatusCode.ShouldBe(HttpStatusCode.UnsupportedMediaType);
        problem.GetProperty("code").GetString().ShouldBe("upload.not_pdf");
    }

    [Fact]
    [Trait("Requirement", "FR-UPL-04")]
    public async Task Upload_IdenticalFile_FlagsDuplicate()
    {
        HttpResponseMessage first = await UploadAsync(PdfBytes, "a.pdf", "dup-1");
        Guid originalId = (await JsonAsync(first)).GetProperty("id").GetGuid();

        HttpResponseMessage second = await UploadAsync(PdfBytes, "b.pdf", "dup-2");
        JsonElement body = await JsonAsync(second);

        body.GetProperty("duplicate").GetProperty("status").GetString().ShouldBe("Suspected");
        body.GetProperty("duplicate").GetProperty("rule").GetString().ShouldBe(DuplicateRule.ExactFile);
        body.GetProperty("duplicate").GetProperty("originalInvoiceId").GetGuid().ShouldBe(originalId);
    }

    [Fact]
    [Trait("Requirement", "FR-DUP-03")]
    public async Task AcknowledgeNotDuplicate_ThenSubmit_Succeeds()
    {
        await UploadAsync(PdfBytes, "a.pdf", "ack-1");
        HttpResponseMessage second = await UploadAsync(PdfBytes, "b.pdf", "ack-2");
        Guid id = (await JsonAsync(second)).GetProperty("id").GetGuid();
        EntityTagHeaderValue etag = second.Headers.ETag.ShouldNotBeNull();

        HttpResponseMessage cleared = await PostAsync(
            new InvoiceId(id), "acknowledge-not-duplicate", etag, new { reason = "Different PO" });
        etag = cleared.Headers.ETag.ShouldNotBeNull();
        await PostAsync(new InvoiceId(id), "enter-manually", etag);
        etag = (await GetAsync(new InvoiceId(id))).Headers.ETag.ShouldNotBeNull();
        await SendAsync(
            HttpMethod.Put,
            $"/api/v1/invoices/{id}/fields",
            etag,
            new
            {
                supplierName = "Acme",
                invoiceNumber = "1",
                invoiceDate = "2026-10-01",
                dueDate = (string?)null,
                currency = "CZK",
                subtotal = (decimal?)null,
                taxAmount = (decimal?)null,
                totalAmount = 100m,
                purchaseOrderReference = (string?)null,
            });
        etag = (await GetAsync(new InvoiceId(id))).Headers.ETag.ShouldNotBeNull();

        HttpResponseMessage submitted = await PostAsync(new InvoiceId(id), "submit", etag);

        submitted.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await JsonAsync(submitted)).GetProperty("status").GetString().ShouldBe("PendingApproval");
    }

    [Fact]
    [Trait("Requirement", "FR-DET-02")]
    public async Task Download_ReturnsPdfAndAudits()
    {
        HttpResponseMessage uploaded = await UploadAsync(PdfBytes, "down.pdf", "dl-1");
        Guid id = (await JsonAsync(uploaded)).GetProperty("id").GetGuid();

        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/invoices/{id}/document");
        request.Headers.Add("X-Development-User", "clerk");
        HttpResponseMessage download = await Client.SendAsync(request, CancellationToken);

        download.StatusCode.ShouldBe(HttpStatusCode.OK);
        download.Content.Headers.ContentType!.MediaType.ShouldBe("application/pdf");
        (await download.Content.ReadAsByteArrayAsync(CancellationToken)).ShouldBe(PdfBytes);

        JsonElement audit = await JsonAsync(await Client.GetAsync($"/api/v1/invoices/{id}/audit", CancellationToken));
        audit.GetProperty("items").EnumerateArray()
            .Select(e => e.GetProperty("action").GetString())
            .ShouldContain("invoice.document_downloaded");
    }

    private async Task<HttpResponseMessage> UploadAsync(byte[] content, string fileName, string idempotencyKey)
    {
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(content);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(file, "file", fileName);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/invoices") { Content = form };
        request.Headers.Add("X-Development-User", "clerk");
        request.Headers.Add("Idempotency-Key", idempotencyKey);
        return await Client.SendAsync(request, CancellationToken);
    }
}
