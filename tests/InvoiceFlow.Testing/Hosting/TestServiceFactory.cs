using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;

namespace InvoiceFlow.Testing.Hosting;

/// <summary>
/// Hosts a service in memory and captures its logs instead of writing them to the console. The catalog and
/// the document storage point at a port nothing listens on, so the service starts but reports itself as not
/// ready.
/// </summary>
public class TestServiceFactory<TEntryPoint> : WebApplicationFactory<TEntryPoint>
    where TEntryPoint : class
{
    private const string UnreachableCatalog = "Host=127.0.0.1;Port=1;Database=catalog;Username=test;Password=test";
    private const string UnreachableStorage = "http://127.0.0.1:1";

    public FakeLogCollector Logs => Services.GetFakeLogCollector();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Catalog", UnreachableCatalog);
        builder.UseSetting("DocumentStorage:ServiceUrl", UnreachableStorage);
        builder.UseSetting("DocumentStorage:AccessKey", "test");
        builder.UseSetting("DocumentStorage:SecretKey", "test");
        builder.ConfigureLogging(logging =>
        {
            logging.ClearProviders();
            logging.AddFakeLogging();
        });
    }
}
