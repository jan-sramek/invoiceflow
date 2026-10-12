using System.Reflection;
using Microsoft.Extensions.Hosting;

namespace InvoiceFlow.ServiceDefaults;

public sealed record ServiceInfo(string Name, string Version)
{
    internal static ServiceInfo From(IHostApplicationBuilder builder)
    {
        string name = builder.Configuration["OTEL_SERVICE_NAME"] is { Length: > 0 } configured
            ? configured
            : builder.Environment.ApplicationName;

        string version = Assembly.Load(new AssemblyName(builder.Environment.ApplicationName))
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown";

        return new ServiceInfo(name, version);
    }
}
