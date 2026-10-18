using System.Globalization;
using InvoiceFlow.Api.Logging;
using InvoiceFlow.Application.Errors;
using InvoiceFlow.Domain.Errors;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace InvoiceFlow.Api.Errors;

/// <summary>
/// Turns expected failures into ProblemDetails with their code; anything else is a bug and gets a generic 500.
/// </summary>
internal sealed partial class InvoiceFlowExceptionHandler(
    IProblemDetailsService problemDetails,
    ILogger<InvoiceFlowExceptionHandler> logger) : IExceptionHandler
{
    // Long enough for a tenant database restart, short enough for the UI's automatic retry
    private const int RetryAfterSeconds = 10;

    public ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        ProblemDetails problem;

        if (exception is InvoiceFlowException expected)
        {
            problem = Describe(expected);
            if (expected.Kind == ErrorKind.Unavailable)
            {
                httpContext.Response.Headers.RetryAfter = RetryAfterSeconds.ToString(CultureInfo.InvariantCulture);
            }
        }
        else
        {
            LogUnexpected(logger, exception);
            problem = new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Extensions = { [ProblemDetailsSetup.CodeKey] = ApiErrors.Server.Unexpected },
            };
        }

        httpContext.Response.StatusCode = problem.Status!.Value;
        return problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
            Exception = exception,
        });
    }

    internal static int StatusOf(ErrorKind kind) => kind switch
    {
        ErrorKind.Validation => StatusCodes.Status400BadRequest,
        ErrorKind.Unauthenticated => StatusCodes.Status401Unauthorized,
        ErrorKind.BusinessRule => StatusCodes.Status422UnprocessableEntity,
        ErrorKind.NotFound => StatusCodes.Status404NotFound,
        ErrorKind.Conflict => StatusCodes.Status409Conflict,
        ErrorKind.Forbidden => StatusCodes.Status403Forbidden,
        ErrorKind.PreconditionFailed => StatusCodes.Status412PreconditionFailed,
        ErrorKind.PreconditionRequired => StatusCodes.Status428PreconditionRequired,
        ErrorKind.PayloadTooLarge => StatusCodes.Status413PayloadTooLarge,
        ErrorKind.UnsupportedMediaType => StatusCodes.Status415UnsupportedMediaType,
        ErrorKind.Unavailable => StatusCodes.Status503ServiceUnavailable,
        _ => StatusCodes.Status500InternalServerError,
    };

    private static ProblemDetails Describe(InvoiceFlowException exception)
    {
        var problem = new ProblemDetails
        {
            Status = StatusOf(exception.Kind),
            Detail = exception.Message,
            Extensions =
            {
                [ProblemDetailsSetup.CodeKey] = exception.Code,
                [ProblemDetailsSetup.ParamsKey] = exception.Params,
            },
        };

        List<FieldError>? errors = exception switch
        {
            InvoiceNotReadyException notReady =>
            [
                .. notReady.Issues.Select(issue => new FieldError(
                    JsonPointer.FromPath(issue.Field.ToString()),
                    issue.Code,
                    new Dictionary<string, object?>())),
            ],
            RequestValidationException invalid =>
            [
                .. invalid.Errors.Select(error => new FieldError(
                    JsonPointer.FromPath(error.Property),
                    error.Code,
                    error.Params)),
            ],
            _ => null,
        };

        if (errors is not null)
        {
            problem.Extensions[ProblemDetailsSetup.ErrorsKey] = errors;
        }

        return problem;
    }

    [LoggerMessage(
        EventId = LogEventIds.UnexpectedError,
        EventName = "UnexpectedError",
        Level = LogLevel.Error,
        Message = "Request failed with an unexpected error")]
    private static partial void LogUnexpected(ILogger logger, Exception exception);
}
