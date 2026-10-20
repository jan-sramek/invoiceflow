namespace InvoiceFlow.Infrastructure.Messaging;

/// <summary>
/// Maps delivery attempt (0 = first) to the next TTL retry routing key, or null when exhausted.
/// </summary>
internal static class RetryRouting
{
    public static string? NextRetryRoutingKey(int attempt) =>
        InvoiceFlowTopology.NextRetryRoutingKey(attempt);

    public static bool IsExhausted(int attempt) =>
        NextRetryRoutingKey(attempt) is null;
}
