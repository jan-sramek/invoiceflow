using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;

namespace InvoiceFlow.ServiceDefaults.Http;

internal sealed class TraceIdHeaderMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Trace-Id";

    public Task InvokeAsync(HttpContext context)
    {
        string? traceId = context.Features.Get<IHttpActivityFeature>()?.Activity.TraceId.ToHexString();

        if (traceId is not null)
        {
            context.Response.OnStarting(() =>
            {
                context.Response.Headers[HeaderName] = traceId;
                return Task.CompletedTask;
            });
        }

        return next(context);
    }
}
