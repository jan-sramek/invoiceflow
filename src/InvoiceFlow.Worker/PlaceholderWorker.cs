namespace InvoiceFlow.Worker;

// Stands in for the RabbitMQ consumers until Stage 5
internal sealed class PlaceholderWorker : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        Task.Delay(Timeout.Infinite, stoppingToken);
}
