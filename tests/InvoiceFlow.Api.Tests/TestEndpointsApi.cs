using InvoiceFlow.Testing.Hosting;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace InvoiceFlow.Api.Tests;

/// <summary>The API plus the controllers of this test project, which exist only to exercise the pipeline.</summary>
public class TestEndpointsApi : TestServiceFactory<Program>
{
    public bool DevelopmentUsers { get; init; } = true;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("Authentication:DevelopmentUsers", DevelopmentUsers ? "true" : "false");
        builder.ConfigureServices(services =>
            services.AddControllers().AddApplicationPart(typeof(TestEndpointsApi).Assembly));
    }
}
