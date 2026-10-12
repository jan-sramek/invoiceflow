using InvoiceFlow.ServiceDefaults.Health;
using InvoiceFlow.ServiceDefaults.Http;
using InvoiceFlow.ServiceDefaults.Logging;
using InvoiceFlow.ServiceDefaults.Telemetry;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace InvoiceFlow.ServiceDefaults;

public static class ServiceDefaultsExtensions
{
    public static TBuilder AddServiceDefaults<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        var service = ServiceInfo.From(builder);
        builder.Services.AddSingleton(service);

        builder.AddDefaultLogging();
        builder.AddDefaultTelemetry(service);
        builder.Services.AddDefaultHealthChecks();

        return builder;
    }

    public static WebApplication UseServiceDefaults(this WebApplication app)
    {
        app.UseMiddleware<TraceIdHeaderMiddleware>();
        app.UseHttpLogging();
        app.MapDefaultHealthChecks();

        return app;
    }
}
