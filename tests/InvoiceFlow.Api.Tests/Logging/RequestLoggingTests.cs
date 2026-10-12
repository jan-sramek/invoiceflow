using Microsoft.Extensions.Logging.Testing;

namespace InvoiceFlow.Api.Tests.Logging;

public class RequestLoggingTests
{
    [Fact]
    public async Task Request_Completed_LogsOneSummaryLine()
    {
        await using ApiFactory api = new();
        using HttpClient client = api.CreateClient();

        await client.GetAsync("/api/v1/missing", TestContext.Current.CancellationToken);
        FakeLogRecord summary = await api.Logs.WaitForAsync(
            record => record.Category == LogAssertions.RequestSummaryCategory,
            TestContext.Current.CancellationToken);

        summary.Value("Method").ShouldBe("GET");
        summary.Value("Path").ShouldBe("/api/v1/missing");
        summary.Value("StatusCode").ShouldBe("404");
        summary.Value("Duration").ShouldNotBeNull();
        api.Logs.GetSnapshot().Count(record => record.Category?.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal) == true).ShouldBe(1);
    }

    [Fact]
    public async Task Request_WithSecretsInQueryAndHeaders_KeepsThemOutOfLogs()
    {
        string queryCanary = LogAssertions.NewCanary("query");
        string headerCanary = LogAssertions.NewCanary("header");
        await using ApiFactory api = new();
        using HttpClient client = api.CreateClient();
        using HttpRequestMessage request = new(HttpMethod.Get, $"/api/v1/missing?token={queryCanary}");
        request.Headers.Add("Authorization", $"Bearer {headerCanary}");

        await client.SendAsync(request, TestContext.Current.CancellationToken);
        await api.Logs.WaitForAsync(
            record => record.Category == LogAssertions.RequestSummaryCategory,
            TestContext.Current.CancellationToken);

        api.Logs.ShouldNotContainAny(queryCanary, headerCanary);
    }

    [Fact]
    public async Task Startup_LogsServiceNameAndVersion()
    {
        await using ApiFactory api = new();
        api.StartServer();

        FakeLogRecord started = await api.Logs.WaitForAsync(
            record => record.Id.Name == "ServiceStarted",
            TestContext.Current.CancellationToken);

        started.Id.Id.ShouldBe(1);
        started.Value("ServiceName").ShouldBe("InvoiceFlow.Api");
        started.Value("ServiceVersion").ShouldNotBeNullOrWhiteSpace();
    }
}
