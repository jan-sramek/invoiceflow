using Microsoft.Extensions.Logging.Testing;

namespace InvoiceFlow.Testing.Logging;

public static class LogAssertions
{
    public const string RequestSummaryCategory = "Microsoft.AspNetCore.HttpLogging.HttpLoggingMiddleware";

    private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(5);

    /// <summary>A unique value to plant in requests or data that must never reach the logs (NFR-SEC-07).</summary>
    public static string NewCanary(string label) => $"canary-{label}-{Guid.NewGuid():N}";

    public static void ShouldNotContainAny(this FakeLogCollector logs, params string[] canaries)
    {
        string[] leaks =
        [
            .. from record in logs.GetSnapshot()
               from canary in canaries
               where Render(record).Contains(canary, StringComparison.OrdinalIgnoreCase)
               select $"{record.Category} (event {record.Id}) contains {canary}",
        ];

        leaks.ShouldBeEmpty();
    }

    // Some records, such as the request summary, are written after the response reaches the client
    public static async Task<FakeLogRecord> WaitForAsync(
        this FakeLogCollector logs,
        Func<FakeLogRecord, bool> predicate,
        CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(WaitTimeout);

        while (true)
        {
            FakeLogRecord? match = logs.GetSnapshot().FirstOrDefault(predicate);
            if (match is not null)
            {
                return match;
            }

            try
            {
                await Task.Delay(20, timeout.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException($"No matching log record within {WaitTimeout.TotalSeconds}s.");
            }
        }
    }

    public static string? Value(this FakeLogRecord record, string key) =>
        record.StructuredState?.FirstOrDefault(pair => pair.Key == key).Value;

    public static string? TraceId(this FakeLogRecord record) =>
        record.Scopes
            .OfType<IEnumerable<KeyValuePair<string, object?>>>()
            .SelectMany(scope => scope)
            .FirstOrDefault(pair => pair.Key == "TraceId")
            .Value?.ToString();

    private static string Render(FakeLogRecord record)
    {
        IEnumerable<string?> parts =
        [
            record.Message,
            record.Exception?.ToString(),
            .. record.StructuredState?.Select(pair => $"{pair.Key}={pair.Value}") ?? [],
            .. record.Scopes.Select(scope => scope?.ToString()),
        ];

        return string.Join('\n', parts);
    }
}
