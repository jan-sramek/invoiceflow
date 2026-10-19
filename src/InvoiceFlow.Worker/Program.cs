using InvoiceFlow.Infrastructure;
using InvoiceFlow.ServiceDefaults;
using InvoiceFlow.ServiceDefaults.Health;
using InvoiceFlow.Worker;

// Kestrel only serves the health endpoints for the Kubernetes probes
WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddMessageTenantContext();
builder.Services.AddScoped<InvoiceProcessingService>();
builder.Services.AddHealthChecks().AddCatalogCheck(HealthEndpoints.ReadyTag);
builder.Services.AddHostedService<InvoiceProcessingConsumer>();

WebApplication app = builder.Build();

app.UseServiceDefaults();

await app.RunAsync();
