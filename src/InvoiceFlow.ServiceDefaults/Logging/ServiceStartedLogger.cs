using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace InvoiceFlow.ServiceDefaults.Logging;

internal sealed partial class ServiceStartedLogger(
    ServiceInfo service,
    IHostEnvironment environment,
    IHostApplicationLifetime lifetime,
    ILogger<ServiceStartedLogger> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        lifetime.ApplicationStarted.Register(() =>
            LogServiceStarted(logger, service.Name, service.Version, environment.EnvironmentName));
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    [LoggerMessage(
        EventId = LogEventIds.ServiceStarted,
        EventName = "ServiceStarted",
        Level = LogLevel.Information,
        Message = "{ServiceName} {ServiceVersion} started in {Environment}")]
    private static partial void LogServiceStarted(ILogger logger, string serviceName, string serviceVersion, string environment);
}
