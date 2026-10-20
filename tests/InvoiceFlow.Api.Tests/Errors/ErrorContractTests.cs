using System.Net;
using System.Text;
using System.Text.Json;
using InvoiceFlow.Api.Errors;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;

namespace InvoiceFlow.Api.Tests.Errors;

public class ErrorContractTests
{
    [Theory]
    [InlineData("/test/errors/validation", HttpStatusCode.BadRequest, "validation.max_length")]
    [InlineData("/test/errors/business-rule", HttpStatusCode.UnprocessableEntity, "invoice.payment_date_in_future")]
    [InlineData("/test/errors/conflict", HttpStatusCode.Conflict, "invoice.invalid_transition")]
    [InlineData("/test/errors/forbidden", HttpStatusCode.Forbidden, "approval.own_invoice")]
    [InlineData("/test/errors/stale", HttpStatusCode.PreconditionFailed, "concurrency.stale_version")]
    [InlineData("/test/errors/unavailable", HttpStatusCode.ServiceUnavailable, "tenant.unavailable")]
    [InlineData("/test/errors/maintenance", HttpStatusCode.ServiceUnavailable, "tenant.maintenance")]
    [InlineData("/test/errors/unexpected", HttpStatusCode.InternalServerError, "server.unexpected")]
    [InlineData("/api/v1/missing", HttpStatusCode.NotFound, "request.not_found")]
    public async Task Error_AnySource_ReturnsProblemDetailsWithCode(string path, HttpStatusCode status, string code)
    {
        await using TestEndpointsApi api = new();
        using HttpClient client = api.CreateClient();

        HttpResponseMessage response = await client.GetAsync(path, TestContext.Current.CancellationToken);
        JsonElement problem = await ProblemAsync(response);

        response.StatusCode.ShouldBe(status);
        problem.GetProperty("status").GetInt32().ShouldBe((int)status);
        problem.GetProperty("code").GetString().ShouldBe(code);
        problem.GetProperty("type").GetString().ShouldBe($"{ErrorCatalogue.DocumentationUrl}#{code}");
        problem.GetProperty("title").GetString().ShouldBe(ErrorCatalogue.Titles[code]);
        problem.GetProperty("instance").GetString().ShouldBe(path);
        problem.GetProperty("traceId").GetString().ShouldBe(response.Headers.GetValues("X-Trace-Id").Single());
    }

    [Fact]
    public async Task ExpectedError_WithParams_ReturnsThem()
    {
        await using TestEndpointsApi api = new();
        using HttpClient client = api.CreateClient();

        JsonElement problem = await ProblemAsync(
            await client.GetAsync("/test/errors/conflict", TestContext.Current.CancellationToken));

        problem.GetProperty("params").GetProperty("from").GetString().ShouldBe("Paid");
        problem.GetProperty("params").GetProperty("action").GetString().ShouldBe("Cancel");
        problem.GetProperty("detail").GetString().ShouldBe("invoice.invalid_transition (from=Paid, action=Cancel)");
    }

    [Fact]
    public async Task ExpectedError_WithoutParams_ReturnsEmptyParams()
    {
        await using TestEndpointsApi api = new();
        using HttpClient client = api.CreateClient();

        JsonElement problem = await ProblemAsync(
            await client.GetAsync("/test/errors/forbidden", TestContext.Current.CancellationToken));

        problem.GetProperty("params").EnumerateObject().ShouldBeEmpty();
    }

    [Fact]
    public async Task NotReadyForSubmission_ReturnsFieldErrors()
    {
        await using TestEndpointsApi api = new();
        using HttpClient client = api.CreateClient();

        JsonElement problem = await ProblemAsync(
            await client.GetAsync("/test/errors/not-ready", TestContext.Current.CancellationToken));

        problem.GetProperty("code").GetString().ShouldBe("invoice.not_ready_for_submission");
        FieldErrors(problem).ShouldBe(
        [
            ("/totalAmount", "validation.total_mismatch"),
            ("/dueDate", "validation.required"),
        ]);
    }

    [Fact]
    public async Task RequestValidationFailed_ReturnsFieldErrorsWithParams()
    {
        await using TestEndpointsApi api = new();
        using HttpClient client = api.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/test/errors/invalid-request", TestContext.Current.CancellationToken);
        JsonElement problem = await ProblemAsync(response);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        problem.GetProperty("code").GetString().ShouldBe("validation.failed");
        FieldErrors(problem).ShouldBe(
        [
            ("/lines/0/amount", "validation.amount_precision"),
            ("/supplierName", "validation.required"),
        ]);
        problem.GetProperty("errors")[0].GetProperty("params").GetProperty("maxDecimals").GetInt32().ShouldBe(2);
    }

    [Fact]
    public async Task Unavailable_TellsClientWhenToRetry()
    {
        await using TestEndpointsApi api = new();
        using HttpClient client = api.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/test/errors/unavailable", TestContext.Current.CancellationToken);
        string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        JsonElement problem = await ProblemAsync(response);

        response.Headers.RetryAfter.ShouldNotBeNull().Delta.ShouldBe(TimeSpan.FromSeconds(10));
        body.ShouldNotContain(ErrorTestController.UnexpectedMessage);
        problem.GetProperty("detail").GetString().ShouldNotBeNull().ShouldNotContain("tenant.unavailable");
    }

    [Fact]
    public async Task UnexpectedError_HidesDetailsAndLogsOnce()
    {
        await using TestEndpointsApi api = new();
        using HttpClient client = api.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/test/errors/unexpected", TestContext.Current.CancellationToken);
        string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        await api.Logs.WaitForAsync(
            record => record.Category == LogAssertions.RequestSummaryCategory,
            TestContext.Current.CancellationToken);

        body.ShouldNotContain(ErrorTestController.UnexpectedMessage);
        body.ShouldNotContain(nameof(InvalidOperationException));
        FakeLogRecord logged = api.Logs.GetSnapshot().Where(record => record.Exception is not null).ShouldHaveSingleItem();
        logged.Id.ShouldBe(new EventId(200, "UnexpectedError"));
        logged.Level.ShouldBe(LogLevel.Error);
        logged.Exception.ShouldBeOfType<InvalidOperationException>();
    }

    [Fact]
    public async Task WrongMethod_ReturnsMethodNotAllowed()
    {
        await using TestEndpointsApi api = new();
        using HttpClient client = api.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/test/errors/body", TestContext.Current.CancellationToken);
        JsonElement problem = await ProblemAsync(response);

        response.StatusCode.ShouldBe(HttpStatusCode.MethodNotAllowed);
        problem.GetProperty("code").GetString().ShouldBe("request.method_not_allowed");
    }

    [Fact]
    public async Task UnsupportedContentType_ReturnsUnsupportedMediaType()
    {
        await using TestEndpointsApi api = new();
        using HttpClient client = api.CreateClient();
        using StringContent content = new("name", Encoding.UTF8, "text/plain");

        HttpResponseMessage response = await client.PostAsync("/test/errors/body", content, TestContext.Current.CancellationToken);
        JsonElement problem = await ProblemAsync(response);

        response.StatusCode.ShouldBe(HttpStatusCode.UnsupportedMediaType);
        problem.GetProperty("code").GetString().ShouldBe("request.unsupported_media_type");
        problem.GetProperty("traceId").GetString().ShouldBe(response.Headers.GetValues("X-Trace-Id").Single());
    }

    [Theory]
    [InlineData("")]
    [InlineData("{")]
    [InlineData("""{"name": "Acme", "lines": [{"amount": 1}""")]
    [InlineData("[]")]
    public async Task Body_NotReadable_ReturnsMalformed(string json)
    {
        await using TestEndpointsApi api = new();
        using HttpClient client = api.CreateClient();

        HttpResponseMessage response = await PostJsonAsync(client, json);
        JsonElement problem = await ProblemAsync(response);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        problem.GetProperty("code").GetString().ShouldBe("request.malformed");
        problem.TryGetProperty("errors", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task Body_WithUnknownProperty_ReturnsFieldError()
    {
        await using TestEndpointsApi api = new();
        using HttpClient client = api.CreateClient();

        JsonElement problem = await ProblemAsync(
            await PostJsonAsync(client, """{"name": "Acme", "lines": [], "colour": "red"}"""));

        problem.GetProperty("code").GetString().ShouldBe("validation.failed");
        FieldErrors(problem).ShouldBe([("/colour", "validation.unknown_property")]);
    }

    [Fact]
    public async Task Body_WithNestedValueOfWrongType_PointsAtIt()
    {
        string canary = LogAssertions.NewCanary("amount");
        await using TestEndpointsApi api = new();
        using HttpClient client = api.CreateClient();

        HttpResponseMessage response = await PostJsonAsync(
            client, $$"""{"name": "Acme", "lines": [{"amount": 1}, {"amount": "{{canary}}"}]}""");
        string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        JsonElement problem = await ProblemAsync(response);

        problem.GetProperty("code").GetString().ShouldBe("validation.failed");
        FieldErrors(problem).ShouldBe([("/lines/1/amount", "validation.invalid_format")]);
        body.ShouldNotContain(canary);
    }

    [Fact]
    public async Task Body_Valid_IsAccepted()
    {
        await using TestEndpointsApi api = new();
        using HttpClient client = api.CreateClient();

        HttpResponseMessage response = await PostJsonAsync(client, """{"name": "Acme", "lines": [{"amount": 1.5}]}""");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private static async Task<HttpResponseMessage> PostJsonAsync(HttpClient client, string json)
    {
        using StringContent content = new(json, Encoding.UTF8, "application/json");
        return await client.PostAsync("/test/errors/body", content, TestContext.Current.CancellationToken);
    }

    private static async Task<JsonElement> ProblemAsync(HttpResponseMessage response)
    {
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using var document = JsonDocument.Parse(body);
        return document.RootElement.Clone();
    }

    private static (string Pointer, string Code)[] FieldErrors(JsonElement problem) =>
    [
        .. problem.GetProperty("errors").EnumerateArray()
            .Select(error => (error.GetProperty("pointer").GetString()!, error.GetProperty("code").GetString()!)),
    ];
}
