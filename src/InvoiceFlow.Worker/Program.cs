using InvoiceFlow.ServiceDefaults;
using InvoiceFlow.Worker;

// Kestrel only serves the health endpoints for the Kubernetes probes
WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddHostedService<PlaceholderWorker>();

WebApplication app = builder.Build();

app.UseServiceDefaults();

await app.RunAsync();
