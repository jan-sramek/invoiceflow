using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;

namespace InvoiceFlow.ServiceDefaults.Http;

public static class ErrorCodeHttpContextExtensions
{
    private static readonly object Key = new();

    /// <summary>
    /// Records the error code of the response on the request's span and its summary log line.
    /// </summary>
    public static void SetErrorCode(this HttpContext context, string code)
    {
        context.Items[Key] = code;
        context.Features.Get<IHttpActivityFeature>()?.Activity.SetTag("error.code", code);
    }

    internal static string? GetErrorCode(this HttpContext context) => context.Items[Key] as string;
}
