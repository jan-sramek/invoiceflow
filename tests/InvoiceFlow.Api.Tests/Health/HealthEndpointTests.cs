using System.Net;

namespace InvoiceFlow.Api.Tests.Health;

public class HealthEndpointTests
{
    [Theory]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    public async Task Get_HealthyService_ReturnsHealthy(string path)
    {
        await using ApiFactory api = new();
        using HttpClient client = api.CreateClient();

        HttpResponseMessage response = await client.GetAsync(path, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).ShouldBe("Healthy");
    }

    [Fact]
    public async Task Get_HealthRequests_AreNotLogged()
    {
        await using ApiFactory api = new();
        using HttpClient client = api.CreateClient();

        await client.GetAsync("/health/live", TestContext.Current.CancellationToken);
        await client.GetAsync("/health/ready", TestContext.Current.CancellationToken);
        await client.GetAsync("/after-health", TestContext.Current.CancellationToken);
        await api.Logs.WaitForAsync(
            record => record.Value("Path") == "/after-health",
            TestContext.Current.CancellationToken);

        api.Logs.GetSnapshot()
            .Where(record => record.Category == LogAssertions.RequestSummaryCategory)
            .Select(record => record.Value("Path"))
            .ShouldBe(["/after-health"]);
    }
}
