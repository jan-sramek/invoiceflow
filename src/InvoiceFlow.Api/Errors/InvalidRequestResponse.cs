using System.Text.Json;
using InvoiceFlow.Application.Errors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace InvoiceFlow.Api.Errors;

/// <summary>
/// Request bodies that cannot be read: a syntax error makes the whole request malformed; a value of the
/// wrong type or a property the API does not know becomes a field error.
/// </summary>
internal static class InvalidRequestResponse
{
    public static IActionResult Create(ActionContext context)
    {
        List<(string Key, ModelError Error)> errors =
        [
            .. from entry in context.ModelState
               from error in entry.Value.Errors
               select (entry.Key, error),
        ];

        // A body that fails to bind also leaves MVC's own "field is required" error for the body parameter
        if (errors.Any(e => e.Error.Exception is JsonException))
        {
            errors.RemoveAll(e => e.Error.Exception is null);
        }

        ProblemDetails problem = errors.Any(e => IsMalformed(e.Key, e.Error))
            ? new ProblemDetails { Extensions = { [ProblemDetailsSetup.CodeKey] = ApiErrors.Request.Malformed } }
            : new ProblemDetails
            {
                Extensions =
                {
                    [ProblemDetailsSetup.CodeKey] = ApplicationErrors.Validation.Failed,
                    [ProblemDetailsSetup.ErrorsKey] = errors
                        .Select(e => new FieldError(
                            JsonPointer.FromPath(e.Key),
                            CodeOf(e.Error),
                            new Dictionary<string, object?>()))
                        .ToList(),
                },
            };

        problem.Status = StatusCodes.Status400BadRequest;
        return new ProblemResult(problem);
    }

    // A missing body has no key; a syntax error wraps the reader's exception, itself a JsonException
    private static bool IsMalformed(string key, ModelError error) =>
        key is "" or "$" || error.Exception is JsonException { InnerException: JsonException };

    // System.Text.Json has no exception type for unmapped members, only this message
    private static string CodeOf(ModelError error) =>
        error.Exception is JsonException exception
        && exception.Message.Contains("could not be mapped", StringComparison.Ordinal)
            ? ApiErrors.Validation.UnknownProperty
            : ApiErrors.Validation.InvalidFormat;

    private sealed class ProblemResult(ProblemDetails problem) : IActionResult
    {
        public Task ExecuteResultAsync(ActionContext context)
        {
            context.HttpContext.Response.StatusCode = problem.Status!.Value;
            return context.HttpContext.RequestServices.GetRequiredService<IProblemDetailsService>()
                .WriteAsync(new ProblemDetailsContext { HttpContext = context.HttpContext, ProblemDetails = problem })
                .AsTask();
        }
    }
}
