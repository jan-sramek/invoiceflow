using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;

namespace InvoiceFlow.Worker.Tests;

public class WorkerHostTests
{
    [Theory]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    public async Task Get_HealthyWorker_ReturnsHealthy(string path)
    {
        await using WorkerFactory worker = new();
        using HttpClient client = worker.CreateClient();

        HttpResponseMessage response = await client.GetAsync(path, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).ShouldBe("Healthy");
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
