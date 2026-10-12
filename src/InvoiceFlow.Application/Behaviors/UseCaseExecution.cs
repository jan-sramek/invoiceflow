using System.Diagnostics;
using InvoiceFlow.Application.Logging;
using InvoiceFlow.Domain.Errors;
using Microsoft.Extensions.Logging;

namespace InvoiceFlow.Application.Behaviors;

/// <summary>
/// Runs a use case in its own span and logs how it ended. Unexpected exceptions are only recorded on the
/// span; the host logs them once where it turns them into a response.
/// </summary>
internal static partial class UseCaseExecution
{
    public const string LoggerCategory = "InvoiceFlow.Application.UseCases";

    public static readonly ActivitySource ActivitySource = new("InvoiceFlow.Application");

    public static async Task<TResult> RunAsync<TResult>(string useCase, ILogger logger, Func<Task<TResult>> handle)
    {
        using Activity? activity = ActivitySource.StartActivity(useCase);
        long started = Stopwatch.GetTimestamp();

        try
        {
            TResult result = await handle();
            double duration = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            LogCompleted(logger, useCase, duration);
            return result;
        }
        catch (InvoiceFlowException exception)
        {
            double duration = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            activity?.SetTag("error.code", exception.Code);
            LogRejected(logger, useCase, exception.Code, duration);
            throw;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            activity?.AddException(exception);
            activity?.SetStatus(ActivityStatusCode.Error);
            throw;
        }
    }

    /// <summary>
    /// <c>GetInvoiceQuery</c> becomes <c>GetInvoice</c>.
    /// </summary>
    public static string NameOf(Type request)
    {
        string name = request.Name;
        foreach (string suffix in (ReadOnlySpan<string>)["Command", "Query"])
        {
            if (name.EndsWith(suffix, StringComparison.Ordinal) && name.Length > suffix.Length)
            {
                return name[..^suffix.Length];
            }
        }

        return name;
    }

    [LoggerMessage(
        EventId = LogEventIds.UseCaseCompleted,
        EventName = "UseCaseCompleted",
        Level = LogLevel.Debug,
        Message = "{UseCase} completed in {Duration} ms")]
    private static partial void LogCompleted(ILogger logger, string useCase, double duration);

    [LoggerMessage(
        EventId = LogEventIds.UseCaseRejected,
        EventName = "UseCaseRejected",
        Level = LogLevel.Information,
        Message = "{UseCase} rejected with {ErrorCode} in {Duration} ms")]
    private static partial void LogRejected(ILogger logger, string useCase, string errorCode, double duration);
}
