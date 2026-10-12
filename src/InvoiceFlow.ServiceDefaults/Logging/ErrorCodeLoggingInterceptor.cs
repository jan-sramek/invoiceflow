using InvoiceFlow.ServiceDefaults.Http;
using Microsoft.AspNetCore.HttpLogging;

namespace InvoiceFlow.ServiceDefaults.Logging;

internal sealed class ErrorCodeLoggingInterceptor : IHttpLoggingInterceptor
{
    public ValueTask OnRequestAsync(HttpLoggingInterceptorContext logContext) => ValueTask.CompletedTask;

    public ValueTask OnResponseAsync(HttpLoggingInterceptorContext logContext)
    {
        if (logContext.HttpContext.GetErrorCode() is { } code)
        {
            logContext.AddParameter("ErrorCode", code);
        }

        return ValueTask.CompletedTask;
    }
}
