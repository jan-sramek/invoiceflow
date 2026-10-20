using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace InvoiceFlow.Infrastructure.Messaging;

/// <summary>
/// Captures and restores W3C <c>traceparent</c> / <c>tracestate</c> across the outbox and RabbitMQ.
/// </summary>
internal static class MessagingTraceContext
{
    public const string TraceParentHeader = "traceparent";
    public const string TraceStateHeader = "tracestate";

    public static readonly ActivitySource ActivitySource = new("InvoiceFlow.Messaging");

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Serialises the current activity's W3C context for storage on <see cref="OutboxMessage.Headers"/>.
    /// </summary>
    public static string? CaptureJson()
    {
        Dictionary<string, string>? headers = Capture();
        return headers is null ? null : JsonSerializer.Serialize(headers, JsonOptions);
    }

    public static Dictionary<string, string>? Capture()
    {
        Activity? activity = Activity.Current;
        if (activity is null || string.IsNullOrEmpty(activity.Id))
        {
            return null;
        }

        var headers = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [TraceParentHeader] = activity.Id,
        };

        if (!string.IsNullOrEmpty(activity.TraceStateString))
        {
            headers[TraceStateHeader] = activity.TraceStateString;
        }

        return headers;
    }

    public static Dictionary<string, object?>? ToRabbitHeaders(string? headersJson)
    {
        Dictionary<string, string>? captured = Parse(headersJson);
        if (captured is null || captured.Count == 0)
        {
            return null;
        }

        return captured.ToDictionary(
            static pair => pair.Key,
            static pair => (object?)pair.Value,
            StringComparer.Ordinal);
    }

    public static Dictionary<string, string>? Parse(string? headersJson)
    {
        if (string.IsNullOrWhiteSpace(headersJson))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(headersJson, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Starts a consumer activity linked to <c>traceparent</c> from RabbitMQ headers when present.
    /// </summary>
    public static Activity? StartConsumerActivity(string name, IDictionary<string, object?>? rabbitHeaders)
    {
        string? traceparent = ReadHeader(rabbitHeaders, TraceParentHeader);
        string? tracestate = ReadHeader(rabbitHeaders, TraceStateHeader);

        if (traceparent is not null
            && ActivityContext.TryParse(traceparent, tracestate, out ActivityContext parent))
        {
            return ActivitySource.StartActivity(name, ActivityKind.Consumer, parent);
        }

        return ActivitySource.StartActivity(name, ActivityKind.Consumer);
    }

    public static string? ReadHeader(IDictionary<string, object?>? headers, string key)
    {
        if (headers is null || !headers.TryGetValue(key, out object? raw) || raw is null)
        {
            return null;
        }

        return raw switch
        {
            string s => s,
            byte[] bytes => Encoding.UTF8.GetString(bytes),
            ReadOnlyMemory<byte> memory => Encoding.UTF8.GetString(memory.Span),
            _ => raw.ToString(),
        };
    }

    /// <summary>
    /// Copies W3C headers from an existing RabbitMQ header bag into a new dictionary (for retry/DLQ).
    /// </summary>
    public static void CopyTraceHeaders(
        IDictionary<string, object?>? source,
        IDictionary<string, object?> destination)
    {
        CopyIfPresent(source, destination, TraceParentHeader);
        CopyIfPresent(source, destination, TraceStateHeader);
    }

    private static void CopyIfPresent(
        IDictionary<string, object?>? source,
        IDictionary<string, object?> destination,
        string key)
    {
        string? value = ReadHeader(source, key);
        if (value is not null)
        {
            destination[key] = value;
        }
    }
}
