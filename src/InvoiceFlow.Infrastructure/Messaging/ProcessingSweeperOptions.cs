using System.ComponentModel.DataAnnotations;

namespace InvoiceFlow.Infrastructure.Messaging;

public sealed class ProcessingSweeperOptions
{
    public const string SectionName = "ProcessingSweeper";

    /// <summary>
    /// Invoices in <c>Processing</c> whose last start/retry audit is older than this are re-queued.
    /// Should exceed the TTL retry budget (10s + 1m + 5m) so in-flight retries are not swept early.
    /// </summary>
    public TimeSpan StuckAfter { get; set; } = TimeSpan.FromMinutes(30);

    public TimeSpan PollInterval { get; set; } = TimeSpan.FromMinutes(1);

    [Range(1, 500)]
    public int BatchSize { get; set; } = 50;
}
