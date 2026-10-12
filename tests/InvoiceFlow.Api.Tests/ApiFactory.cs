using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Logging;

namespace InvoiceFlow.Api.Tests;

public sealed class ApiFactory : WebApplicationFactory<Program>
{
    public FakeLogCollector Logs => Services.GetFakeLogCollector();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureLogging(logging =>
        {
            logging.ClearProviders();
            logging.AddFakeLogging();
        });
    }
}
