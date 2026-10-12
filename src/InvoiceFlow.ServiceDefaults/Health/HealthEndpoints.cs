using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace InvoiceFlow.ServiceDefaults.Health;

public static class HealthEndpoints
{
    public const string LivePath = "/health/live";
    public const string ReadyPath = "/health/ready";

    /// <summary>Liveness checks must never depend on external services.</summary>
    public const string LiveTag = "live";

    public const string ReadyTag = "ready";

    internal static bool IsHealthRequest(PathString path) =>
        path.StartsWithSegments("/health", StringComparison.OrdinalIgnoreCase);

    internal static void AddDefaultHealthChecks(this IServiceCollection services)
    {
        services.AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy(), [LiveTag]);
    }

    internal static void MapDefaultHealthChecks(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapHealthCheck(LivePath, LiveTag);
        endpoints.MapHealthCheck(ReadyPath, ReadyTag);
    }

    private static void MapHealthCheck(this IEndpointRouteBuilder endpoints, string path, string tag)
    {
        endpoints
            .MapHealthChecks(path, new HealthCheckOptions { Predicate = check => check.Tags.Contains(tag) })
            .AllowAnonymous()
            .DisableHttpMetrics()
            .WithHttpLogging(HttpLoggingFields.None);
    }
}
