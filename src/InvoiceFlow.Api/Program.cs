using InvoiceFlow.ServiceDefaults;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

WebApplication app = builder.Build();

app.UseServiceDefaults();

await app.RunAsync();
