using Microsoft.Extensions.Logging.Testing;

namespace InvoiceFlow.Api.Tests.Http;

public class TraceIdHeaderTests
{
    [Theory]
    [InlineData("/health/live")]
    [InlineData("/api/v1/missing")]
    public async Task Response_AnyRequest_CarriesTraceId(string path)
    {
        await using ApiFactory api = new();
        using HttpClient client = api.CreateClient();

        HttpResponseMessage response = await client.GetAsync(path, TestContext.Current.CancellationToken);

        string traceId = response.Headers.GetValues("X-Trace-Id").ShouldHaveSingleItem();
        traceId.ShouldMatch("^[0-9a-f]{32}$");
        traceId.ShouldNotBe(new string('0', 32));
    }

    [Fact]
    public async Task Response_TraceId_MatchesRequestLog()
    {
        await using ApiFactory api = new();
        using HttpClient client = api.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/api/v1/missing", TestContext.Current.CancellationToken);
        FakeLogRecord summary = await api.Logs.WaitForAsync(
            record => record.Category == LogAssertions.RequestSummaryCategory,
            TestContext.Current.CancellationToken);

        summary.TraceId().ShouldBe(response.Headers.GetValues("X-Trace-Id").Single());
    }
}
