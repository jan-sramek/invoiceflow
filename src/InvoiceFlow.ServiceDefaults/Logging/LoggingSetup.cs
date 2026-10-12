using Microsoft.AspNetCore.HttpLogging;
using Microsoft.Extensions.Compliance.Redaction;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace InvoiceFlow.ServiceDefaults.Logging;

internal static class LoggingSetup
{
    public static void AddDefaultLogging(this IHostApplicationBuilder builder)
    {
        builder.Logging.ClearProviders();
        builder.Logging.AddJsonConsole(options =>
        {
            options.IncludeScopes = true;
            options.UseUtcTimestamp = true;
            options.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ";
        });

        // Anything classified as sensitive is erased unless a category gets its own redactor
        builder.Logging.EnableRedaction();
        builder.Services.AddRedaction(redaction => redaction.SetFallbackRedactor<ErasingRedactor>());

        // One summary line per request; query strings, headers and bodies stay out of the logs
        builder.Services.AddHttpLogging(options =>
        {
            options.LoggingFields = HttpLoggingFields.RequestMethod
                | HttpLoggingFields.RequestPath
                | HttpLoggingFields.ResponseStatusCode
                | HttpLoggingFields.Duration;
            options.CombineLogs = true;
        });

        builder.Services.AddHostedService<ServiceStartedLogger>();
    }
}
