using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;

namespace InvoiceFlow.Worker.Tests;

public class WorkerHostTests
{
    [Fact]
    public async Task GetLive_CatalogUnreachable_ReturnsHealthy()
    {
        await using WorkerFactory worker = new();
        using HttpClient client = worker.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/health/live", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).ShouldBe("Healthy");
    }

    [Fact]
    public async Task GetReady_CatalogUnreachable_ReturnsUnhealthy()
    {
        await using WorkerFactory worker = new();
        using HttpClient client = worker.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/health/ready", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).ShouldBe("Unhealthy");
    }

    [Fact]
    public async Task Startup_LogsServiceNameAndVersion()
    {
        await using WorkerFactory worker = new();
        worker.StartServer();

        FakeLogRecord started = await worker.Logs.WaitForAsync(
            record => record.Id.Name == "ServiceStarted",
            TestContext.Current.CancellationToken);

        started.Value("ServiceName").ShouldBe("InvoiceFlow.Worker");
    }

    [Fact]
    public async Task Shutdown_StopsBackgroundWorkWithoutErrors()
    {
        WorkerFactory worker = new();
        worker.StartServer();
        PlaceholderWorker background = worker.Services.GetServices<IHostedService>().OfType<PlaceholderWorker>().Single();
        FakeLogCollector logs = worker.Logs;

        background.ExecuteTask.ShouldNotBeNull().IsCompleted.ShouldBeFalse();

        await worker.DisposeAsync();

        background.ExecuteTask.IsCompleted.ShouldBeTrue();
        logs.GetSnapshot().ShouldNotContain(record => record.Level >= LogLevel.Error);
    }
}
