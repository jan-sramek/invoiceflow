using InvoiceFlow.Application.Logging;
using Microsoft.Extensions.Logging;

namespace InvoiceFlow.Application.Behaviors;

/// <summary>
/// Undo steps for effects the database transaction cannot roll back, such as a stored document. They run
/// in reverse order when the command or its commit fails. A failing step is logged and does not stop the rest.
/// </summary>
internal sealed partial class Compensations(ILoggerFactory loggerFactory)
{
    private readonly ILogger _logger = loggerFactory.CreateLogger(UseCaseExecution.LoggerCategory);
    private readonly List<Func<Task>> _steps = [];

    public void Add(Func<Task> step) => _steps.Add(step);

    public async Task RunAsync()
    {
        for (int i = _steps.Count - 1; i >= 0; i--)
        {
            try
            {
                await _steps[i]();
            }
            catch (Exception exception)
            {
                LogFailed(_logger, exception);
            }
        }

        _steps.Clear();
    }

    [LoggerMessage(
        EventId = LogEventIds.CompensationFailed,
        EventName = "CompensationFailed",
        Level = LogLevel.Warning,
        Message = "Could not undo a side effect of a failed command")]
    private static partial void LogFailed(ILogger logger, Exception exception);
}
