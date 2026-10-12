using System.Diagnostics;
using InvoiceFlow.ServiceDefaults.Http;
using Microsoft.AspNetCore.Mvc;

namespace InvoiceFlow.Api.Errors;

/// <summary>
/// One error contract for every non-2xx response: thrown exceptions, invalid request bodies and framework
/// responses without a body all leave as ProblemDetails with a <c>code</c> and a <c>traceId</c>.
/// </summary>
internal static class ProblemDetailsSetup
{
    public const string CodeKey = "code";
    public const string ParamsKey = "params";
    public const string ErrorsKey = "errors";
    public const string TraceIdKey = "traceId";

    private const string GenericServerDetail = "The request could not be completed. Quote the trace id when reporting it.";

    public static IServiceCollection AddErrorContract(this IServiceCollection services)
    {
        services.AddProblemDetails(options => options.CustomizeProblemDetails = Complete);
        services.AddExceptionHandler<InvoiceFlowExceptionHandler>();
        services.PostConfigure<ApiBehaviorOptions>(options =>
            options.InvalidModelStateResponseFactory = InvalidRequestResponse.Create);

        return services;
    }

    public static WebApplication UseErrorContract(this WebApplication app)
    {
        app.UseExceptionHandler();
        app.UseStatusCodePages();

        return app;
    }

    private static void Complete(ProblemDetailsContext context)
    {
        ProblemDetails problem = context.ProblemDetails;
        int status = problem.Status ?? context.HttpContext.Response.StatusCode;
        string code = problem.Extensions.TryGetValue(CodeKey, out object? value) && value is string known
            ? known
            : DefaultCode(status);

        problem.Status = status;
        problem.Type = ErrorCatalogue.TypeOf(code);
        problem.Title = ErrorCatalogue.Titles.GetValueOrDefault(code, problem.Title ?? string.Empty);
        problem.Instance = context.HttpContext.Request.Path;

        if (status >= StatusCodes.Status500InternalServerError)
        {
            problem.Detail = GenericServerDetail;
        }

        // Re-added so that traceId always comes last, after code, params and errors
        problem.Extensions.Remove(CodeKey);
        problem.Extensions.Remove(TraceIdKey);
        problem.Extensions = new Dictionary<string, object?>(
            [new(CodeKey, code), .. problem.Extensions, new(TraceIdKey, TraceId(context.HttpContext))]);

        context.HttpContext.SetErrorCode(code);
    }

    // Responses the framework produces without a body, such as an unknown route
    private static string DefaultCode(int status) => status switch
    {
        StatusCodes.Status404NotFound => ApiErrors.Request.NotFound,
        StatusCodes.Status405MethodNotAllowed => ApiErrors.Request.MethodNotAllowed,
        StatusCodes.Status415UnsupportedMediaType => ApiErrors.Request.UnsupportedMediaType,
        >= StatusCodes.Status500InternalServerError => ApiErrors.Server.Unexpected,
        _ => ApiErrors.Request.Invalid,
    };

    // Same value as the X-Trace-Id header, so the id the user quotes finds the trace and the logs
    private static string TraceId(HttpContext context) =>
        Activity.Current?.TraceId.ToHexString() ?? context.TraceIdentifier;
}
