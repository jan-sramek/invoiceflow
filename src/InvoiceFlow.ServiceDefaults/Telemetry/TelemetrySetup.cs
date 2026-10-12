using InvoiceFlow.ServiceDefaults.Health;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace InvoiceFlow.ServiceDefaults.Telemetry;

internal static class TelemetrySetup
{
    public static void AddDefaultTelemetry(this IHostApplicationBuilder builder, ServiceInfo service)
    {
        // Tracing stays on without an exporter so trace ids still reach logs and the response header
        bool export = !string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]);

        builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(service.Name, serviceVersion: service.Version))
            .WithTracing(tracing =>
            {
                tracing
                    .AddSource("InvoiceFlow.*")
                    .AddAspNetCoreInstrumentation(options =>
                        options.Filter = context => !HealthEndpoints.IsHealthRequest(context.Request.Path))
                    .AddHttpClientInstrumentation();

                if (export)
                {
                    tracing.AddOtlpExporter();
                }
            })
            .WithMetrics(metrics =>
            {
                metrics
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation();

                if (export)
                {
                    metrics.AddOtlpExporter();
                }
            });
    }
}
