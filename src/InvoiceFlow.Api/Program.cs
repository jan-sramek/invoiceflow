using InvoiceFlow.Infrastructure;
using InvoiceFlow.ServiceDefaults;
using InvoiceFlow.ServiceDefaults.Health;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddHealthChecks().AddCatalogCheck(HealthEndpoints.ReadyTag);

WebApplication app = builder.Build();

app.UseServiceDefaults();

await app.RunAsync();
