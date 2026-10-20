namespace InvoiceFlow.Infrastructure.Messaging;

internal static class InvoiceFlowTopology
{
    public const string EventsExchange = "invoiceflow.events";
    public const string RetryExchange = "invoiceflow.retry";
    public const string DlxExchange = "invoiceflow.dlx";

    public const string ProcessingQueue = "invoice-processing";
    public const string DeadLetterQueue = "invoice-processing.dlq";

    public const string UploadedRoutingKey = "invoice.uploaded";
    public const string DeadLetterRoutingKey = "invoice.dead";

    public const string AttemptHeader = "x-invoiceflow-attempt";

    public static readonly RetryTier[] RetryTiers =
    [
        new("invoice-processing.retry.10s", "retry.10s", TimeSpan.FromSeconds(10)),
        new("invoice-processing.retry.1m", "retry.1m", TimeSpan.FromMinutes(1)),
        new("invoice-processing.retry.5m", "retry.5m", TimeSpan.FromMinutes(5)),
    ];

    public static string? NextRetryRoutingKey(int attemptZeroBased) =>
        attemptZeroBased >= 0 && attemptZeroBased < RetryTiers.Length
            ? RetryTiers[attemptZeroBased].RoutingKey
            : null;

    internal sealed record RetryTier(string QueueName, string RoutingKey, TimeSpan Ttl);
}
